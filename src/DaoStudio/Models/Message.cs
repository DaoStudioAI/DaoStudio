using System;
using System.Collections.Generic;
using System.Linq;
using DaoStudio.Interfaces;

namespace DaoStudio
{
    /// <summary>
    /// Binary data wrapper that implements IMsgBinaryData
    /// </summary>
    internal class MsgBinaryData : DBStorage.Models.BinaryData, IMsgBinaryData
    {
        // Implement Type interface property with proper type conversion
        MsgBinaryDataType IMsgBinaryData.Type
        {
            get => (MsgBinaryDataType)(int)base.Type;
            set => base.Type = (int)value;
        }

        /// <summary>
        /// Creates a new DaoStudio.Models.MsgBinaryData from a DBStorage.Models.MsgBinaryData
        /// </summary>
        /// <param name="dbBinaryData">The DBStorage MsgBinaryData to convert</param>
        /// <returns>A new DaoStudio.Models.MsgBinaryData instance</returns>
        public static MsgBinaryData FromDBBinaryData(DBStorage.Models.BinaryData dbBinaryData)
        {
            if (dbBinaryData == null)
                throw new ArgumentNullException(nameof(dbBinaryData));

            var binaryData = new MsgBinaryData
            {
                Name = dbBinaryData.Name,
                Data = dbBinaryData.Data
            };
            
            // Set the Type through the base class property
            binaryData.Type = dbBinaryData.Type;

            return binaryData;
        }

        /// <summary>
        /// Converts to DBStorage.Models.MsgBinaryData
        /// </summary>
        /// <returns>A new DBStorage.Models.MsgBinaryData instance</returns>
        public DBStorage.Models.BinaryData ToDBBinaryData()
        {
            return new DBStorage.Models.BinaryData
            {
                Name = this.Name,
                Type = this.Type,
                Data = this.Data
            };
        }
    }

    /// <summary>
    /// Message wrapper that extends DBStorage Message and implements IMessage
    /// </summary>
    internal class Message : DBStorage.Models.Message, IMessage
    {
        // Implement Role interface property with proper type conversion
        MessageRole IMessage.Role
        {
            get => (MessageRole)(int)base.Role;
            set => base.Role = (int)value;
        }

        // Implement Type interface property with proper type conversion  
        MessageType IMessage.Type
        {
            get => (MessageType)(int)base.Type;
            set => base.Type = (int)value;
        }

        // Override BinaryContents to use the interface type
        private List<IMsgBinaryData>? _binaryContents;

        /// <summary>
        /// Binary contents associated with the message (interface type)
        /// </summary>
        public new List<IMsgBinaryData>? BinaryContents 
        { 
            get => _binaryContents;
            set => _binaryContents = value;
        }

        /// <summary>
        /// Creates a new DaoStudio.Models.Message from a DBStorage.Models.Message
        /// </summary>
        /// <param name="dbMessage">The DBStorage Message to convert</param>
        /// <returns>A new DaoStudio.Models.Message instance</returns>
        public static Message FromDBMessage(DBStorage.Models.Message dbMessage)
        {
            if (dbMessage == null)
                throw new ArgumentNullException(nameof(dbMessage));

            var message = new Message
            {
                Id = dbMessage.Id,
                SessionId = dbMessage.SessionId,
                Content = dbMessage.Content,
                Role = dbMessage.Role,
                Type = dbMessage.Type,
                BinaryVersion = dbMessage.BinaryVersion,
                ParentMsgId = dbMessage.ParentMsgId,
                ParentSessId = dbMessage.ParentSessId,
                CreatedAt = dbMessage.CreatedAt,
                LastModified = dbMessage.LastModified
            };

            // Convert binary contents
            if (dbMessage.BinaryContents != null)
            {
                message.BinaryContents = dbMessage.BinaryContents
                    .Select(bc => MsgBinaryData.FromDBBinaryData(bc) as IMsgBinaryData)
                    .ToList();
            }

            return message;
        }

        /// <summary>
        /// Converts to DBStorage.Models.Message
        /// </summary>
        /// <returns>A new DBStorage.Models.Message instance</returns>
        public DBStorage.Models.Message ToDBMessage()
        {
            var dbMessage = new DBStorage.Models.Message
            {
                Id = this.Id,
                SessionId = this.SessionId,
                Content = this.Content,
                Role = this.Role,
                Type = this.Type,
                BinaryVersion = this.BinaryVersion,
                ParentMsgId = this.ParentMsgId,
                ParentSessId = this.ParentSessId,
                CreatedAt = this.CreatedAt,
                LastModified = this.LastModified
            };

            // Convert binary contents
            if (this.BinaryContents != null)
            {
                dbMessage.BinaryContents = this.BinaryContents
                    .Select(bc => ((MsgBinaryData)bc).ToDBBinaryData())
                    .ToList();
            }

            return dbMessage;
        }

        /// <summary>
        /// Adds binary data to the message
        /// </summary>
        /// <param name="name">Name of the binary data</param>
        /// <param name="type">Type of the binary data</param>
        /// <param name="data">Binary data content</param>
        public void AddBinaryData(string name, MsgBinaryDataType type, byte[] data)
        {
            if (BinaryContents == null)
            {
                BinaryContents = new List<IMsgBinaryData>();
            }

            var binaryData = new MsgBinaryData
            {
                Name = name,
                Data = data,
                Type = (int)type
            };
            
            BinaryContents.Add(binaryData);
        }
    }
}