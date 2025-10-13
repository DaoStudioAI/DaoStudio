namespace DaoStudio.Interfaces
{
    /// <summary>
    /// Defines the type of tool operation performed
    /// </summary>
    public enum ToolOperationType
    {
        Created,
        Updated,
        Deleted
    }

    /// <summary>
    /// Defines the type of tool list update performed
    /// </summary>
    public enum ToolListUpdateType
    {
        Added,
        Removed
    }

    /// <summary>
    /// Event arguments for tool operation events
    /// </summary>
    public class ToolOperationEventArgs : EventArgs
    {
        public ToolOperationType OperationType { get; set; }
        public ITool? Tool { get; set; }
        public long? ToolId { get; set; }

        /// <summary>
        /// Constructor for Create/Update operations
        /// </summary>
        /// <param name="operationType">The type of operation</param>
        /// <param name="tool">The tool involved in the operation</param>
        public ToolOperationEventArgs(ToolOperationType operationType, ITool tool)
        {
            OperationType = operationType;
            Tool = tool;
            ToolId = tool.Id;
        }

        /// <summary>
        /// Constructor for Delete operations
        /// </summary>
        /// <param name="toolId">The ID of the deleted tool</param>
        public ToolOperationEventArgs(long toolId)
        {
            OperationType = ToolOperationType.Deleted;
            ToolId = toolId;
            Tool = null;
        }
    }

    /// <summary>
    /// Event arguments for tool list update events
    /// </summary>
    public class ToolListUpdateEventArgs : EventArgs
    {
        public ToolListUpdateType UpdateType { get; set; }
        public ITool? Tool { get; set; }
        public long? ToolId { get; set; }
        public int? TotalToolCount { get; set; }

        /// <summary>
        /// Constructor for tool list update events
        /// </summary>
        /// <param name="updateType">The type of list update</param>
        /// <param name="tool">The tool that was added/removed (null for removed tools)</param>
        /// <param name="totalToolCount">The total number of tools after the update</param>
        public ToolListUpdateEventArgs(ToolListUpdateType updateType, ITool? tool = null, int? totalToolCount = null)
        {
            UpdateType = updateType;
            Tool = tool;
            ToolId = tool?.Id;
            TotalToolCount = totalToolCount;
        }

        /// <summary>
        /// Constructor for tool removal events with just tool ID
        /// </summary>
        /// <param name="updateType">The type of list update</param>
        /// <param name="toolId">The ID of the removed tool</param>
        /// <param name="totalToolCount">The total number of tools after the update</param>
        public ToolListUpdateEventArgs(ToolListUpdateType updateType, long toolId, int? totalToolCount = null)
        {
            UpdateType = updateType;
            ToolId = toolId;
            Tool = null;
            TotalToolCount = totalToolCount;
        }
    }
}