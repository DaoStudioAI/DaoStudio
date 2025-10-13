using DaoStudio.Common.Plugins;
using DaoStudio.DBStorage.Models;
using DaoStudio.Interfaces;
using DaoStudio.Interfaces.Plugins;
using System;
using System.Collections.Generic;

namespace DaoStudio
{
    /// <summary>
    /// Concrete implementation of ITool interface
    /// </summary>
    internal class Tool : LlmTool, ITool
    {
        /// <summary>
        /// Creates a new empty Tool instance for external callers
        /// </summary>
        public Tool()
        {
            Parameters = new Dictionary<string, string>();
            Functions = null;
            Name = string.Empty;
            Description = string.Empty;
            StaticId = string.Empty;
            ToolConfig = string.Empty;
            DevMsg = string.Empty;
            IsEnabled = true;
            ToolType = (int)DaoStudio.Interfaces.ToolType.Normal;
            State = (int)DaoStudio.Interfaces.ToolState.Stateless;
            CreatedAt = DateTime.UtcNow;
            LastModified = DateTime.UtcNow;
        }

        internal Tool(LlmTool source, List<FunctionWithDescription>? functions = null)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            
            // Copy all properties from source
            Id = source.Id;
            StaticId = source.StaticId;
            Name = source.Name;
            Description = source.Description;
            ToolConfig = source.ToolConfig;
            ToolType = source.ToolType;
            Parameters = source.Parameters ?? new Dictionary<string, string>();
            IsEnabled = source.IsEnabled;
            LastModified = source.LastModified;
            CreatedAt = source.CreatedAt;
            DevMsg = source.DevMsg;
            State = source.State;
            StateData = source.StateData;
            AppId = source.AppId;

            Functions = functions;
        }

        public List<FunctionWithDescription>? Functions { get; }

        // Explicit interface implementations to handle type mismatches
        Interfaces.ToolType ITool.ToolType 
        { 
            get => (Interfaces.ToolType)(int)base.ToolType;
            set => base.ToolType = (int)value;
        }
        
        Interfaces.ToolState ITool.State 
        { 
            get => (Interfaces.ToolState)(int)base.State;
            set => base.State = (int)value;
        }


    }
}
