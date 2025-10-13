using DaoStudio.Common;
using DaoStudio.DBStorage.Interfaces;
using DaoStudio.DBStorage.Models;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using DaoStudio.Properties;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Linq;

namespace DaoStudio;

internal partial class Session : ISession
{

    private SemaphoreSlim _sendMessageSemaphore = new SemaphoreSlim(1, 1);

    public event EventHandler<MessageChangedEventArgs>? OnMessageChanged;


    private Dictionary<string, List<FunctionWithDescription>>? _availablePlugin;

    // Accumulated Token count properties
    public long TotalTokenCount => dbsess.TotalTokenCount;
    public long InputTokenCount => dbsess.InputTokenCount;
    public long OutputTokenCount => dbsess.OutputTokenCount;
    public Dictionary<string, string>? AdditionalTokenProperties
    {
        get
        {
            if (dbsess.Properties == null)
            {
                return null;
            }

            // Safely attempt to retrieve the JSON string for additional counts
            if (!dbsess.Properties.TryGetValue(SessionPropertiesNames.AdditionalCounts, out var json) ||
                string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            }
            catch (JsonException)
            {
                // If the stored JSON is malformed, ignore it gracefully
                return null;
            }
        }
    }

    public event EventHandler<Interfaces.UsageDetails>? UsageDetailsReceived;

    private CancellationTokenSource? _cancellationTokenSource;
    public CancellationTokenSource? CurrentCancellationToken => _cancellationTokenSource;

    public int MsgMaxLoopCount { get; set; }




    /// <summary>
    /// Sends a message to the LLM and streams the response with real-time notifications
    /// </summary>
    /// <param name="userMessage">The message text from the user</param>
    /// <returns>The final response message from the LLM</returns>
    public async Task<IMessage> SendMessageAsync(string userMessage)
    {
        // Validate arguments
        if (userMessage is null)
        {
            throw new ArgumentNullException(nameof(userMessage));
        }
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            throw new UIException(Resources.Error_UserMessageEmpty);
        }


        // Ensure session is properly initialized
        await EnsureInitializedAsync();


        // Ensure only one call to SendMessageAsync can run at a time
        var myturn = await _sendMessageSemaphore.WaitAsync(3000).ConfigureAwait(false);
        if (!myturn)
        {
            throw new TimeoutException("Timeout waiting for semaphore");
        }
        CancellationTokenSource? presrc = null;
        lock (this)
        {
            presrc = _cancellationTokenSource;
            _cancellationTokenSource = new CancellationTokenSource();
        }
        presrc?.CancelAsync();

        var token = _cancellationTokenSource.Token;

        try
        {
            // Set session status to Sending
            SessionStatus = SessionStatus.Sending;

            IMessage? res = null;

            var userMsg = await messageService.CreateMessageAsync(userMessage, MessageRole.User, MessageType.Normal, Id, true);
            if (userMsg == null)
            {
                throw new OperationCanceledException("User message creation returned null.");
            }
            await FireMessageChangedAsync(userMsg, MessageChangeType.New);



            IMessage curtextmsg = await messageService.CreateMessageAsync(string.Empty, MessageRole.Assistant, MessageType.Normal, Id, false);
            if (curtextmsg == null)
            {
                throw new OperationCanceledException("Assistant message creation returned null.");
            }
            IMessage? firstmsg=null;
            bool first = true;

            for (var i = 0; i < MsgMaxLoopCount; ++i)
            {
                // Get message history as IMessage list
                var messages = (await messageService.GetMessagesBySessionIdAsync(Id)).ToList();

                // Add system message if needed
                if (!string.IsNullOrWhiteSpace(person?.DeveloperMessage))
                {
                    var systemMessage = await messageService.CreateMessageAsync(person.DeveloperMessage, MessageRole.System, MessageType.Normal, Id, false);
                    messages.Insert(0, systemMessage);
                }

                await RunFiltersAsync(FilterType.PreProcessing, messages, token);

                // Get streaming response from engine
                var responseStream = await engine!.GetMessageAsync(messages, _availablePlugin, this, token);

                bool shouldcontinue = false;

                await foreach (var responseMessage in responseStream)
                {
                    token.ThrowIfCancellationRequested();

                    // Handle streaming response
                    if (responseMessage != null)
                    {
                        // Set the session ID for the response message
                        responseMessage.SessionId = Id;
                        if ((responseMessage.BinaryContents?.Count > 0))
                        {
                            await messageService.SaveMessageAsync(responseMessage, true);
                            messages.Add(responseMessage);
                            if (responseMessage.BinaryContents?.Any(
                                bc => (bc.Type == MsgBinaryDataType.ToolCall)|| bc.Type == MsgBinaryDataType.ToolCallResult) 
                                == true)
                            {
                                await RunFiltersAsync(FilterType.PostProcessing, messages, token);
                            }
                            await FireMessageChangedAsync(responseMessage, MessageChangeType.New);
                            curtextmsg = await messageService.CreateMessageAsync(string.Empty, MessageRole.Assistant, MessageType.Normal, Id, false);
                            first = true;
                            if (firstmsg == null)
                            {
                                firstmsg = responseMessage;
                            }

                            if (responseMessage.BinaryContents?.Any(
                                bc => (bc.Type == MsgBinaryDataType.ToolCall) || bc.Type == MsgBinaryDataType.ToolCallResult)
                                == true)
                            {
                                await RunFiltersAsync(FilterType.PreProcessing, messages, token);
                            }
                        }
                        else if (!string.IsNullOrEmpty(responseMessage.Content))
                        {
                            curtextmsg.Content += responseMessage.Content;
                            await messageService.SaveMessageAsync(curtextmsg, true);
                            await FireMessageChangedAsync(curtextmsg, first ? MessageChangeType.New : MessageChangeType.Updated);
                            first = false;
                        }
                        if (firstmsg == null)
                        {
                            firstmsg = curtextmsg;
                        }


                        // Check for tool calls in binary contents
                        if (responseMessage.BinaryContents?.Any(bc => bc.Type == MsgBinaryDataType.ToolCall) == true)
                        {
                            shouldcontinue = true; 
                        }
                        else
                        {
                            shouldcontinue = false;
                        }
                    }
                }

                res = curtextmsg;
                
                messages = (await messageService.GetMessagesBySessionIdAsync(Id).ConfigureAwait(false)).ToList();
                await RunFiltersAsync(FilterType.PostProcessing, messages, token);

                await UpdateSessionLastModifiedAsync();

                if (!shouldcontinue)
                {
                    break;
                }
            }
            await FireMessageChangedAsync(firstmsg?? userMsg, MessageChangeType.Finished);
            return res ?? await messageService.CreateMessageAsync(string.Empty, MessageRole.Assistant, MessageType.Normal, Id, false);
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("SendMessageAsync operation was cancelled for session {SessionId}", Id);
            // Ensure session status is set to Idle on cancellation
            SessionStatus = SessionStatus.Idle;
            throw;
        }
        catch (System.ClientModel.ClientResultException e)
        {
            // Attempt to extract provider raw response details and include them in the UI exception
            string details;
            try
            {
                var raw = e.GetRawResponse();
                // raw may not be a string - convert safely
                if (raw == null)
                {
                    details = e.Message;
                }
                else
                {
                    var contentObj = raw.Content;
                    //todo: raw.Content is System.BinaryData
                    details = contentObj.ToString();
                }
            }
            catch (Exception ex)
            {
                // If extracting raw response fails, fall back to the exception message
                details = e.Message;
                logger.LogDebug(ex, "Failed to get raw response from ClientResultException for session {SessionId}", Id);
            }

            logger.LogError(e, "LLM provider returned error for session {SessionId}: {Details}", Id, details);
            throw new UIException(string.Format(Resources.Error_LlmCommunication, details));

        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during message processing for session {SessionId}", Id);
            throw new UIException(string.Format(Resources.Error_LlmCommunication, ex.Message));
        }
        finally
        {
            // Set session status back to Idle
            SessionStatus = SessionStatus.Idle;

            // Always release the semaphore
            _sendMessageSemaphore.Release();
        }
    }



    public Task FireMessageChangedAsync(IMessage message, MessageChangeType change)
    {
        var handlers = OnMessageChanged;
        if (handlers == null)
        {
            return Task.CompletedTask;
        }
        Task.Run(() =>
        {
            var args = new MessageChangedEventArgs(message, change);
            foreach (EventHandler<MessageChangedEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex)
                {
                    this.logger.LogError(ex, "Error in OnMessageChanged handler for session {SessionId}", Id);
                }
            }
        });
        return Task.CompletedTask;
    }


}
