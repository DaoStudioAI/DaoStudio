# NamingTool

**Purpose**: Arbitrarily redefining a concept and acting on the new definition

The NamingTool is a powerful DaoStudio plugin that enables dynamic task subdivision and parallel execution of AI sessions. It allows users to create configurable functions that can spawn child AI sessions with custom parameters, prompt templates, and execution strategies.

## Table of Contents
- [Overview](#overview)
- [Core Workflow](#core-workflow)
- [Configuration](#configuration)
- [Parallel Execution](#parallel-execution)
- [Error Handling & DanglingBehavior](#error-handling--danglingbehavior)
- [Scriban Template Support](#scriban-template-support)
- [Usage Examples](#usage-examples)
- [Architecture](#architecture)
- [API Reference](#api-reference)

## Overview

The NamingTool provides a flexible framework for:
- **Dynamic Function Definition**: Create custom functions with configurable names, descriptions, and parameters
- **Task Subdivision**: Break down complex tasks into smaller, manageable subtasks
- **Parallel Processing**: Execute multiple AI sessions concurrently with different parameters
- **Template-based Prompts**: Use Scriban templates for dynamic prompt generation
- **Recursion Control**: Manage depth limits to prevent infinite task subdivision

## Core Workflow

The NamingTool follows this refined workflow:

### 1. **Tool Configuration**
   - Create a `PlugToolInfo` instance
   - Configure `PlugToolInfo.Config` with `NamingConfig` settings
   - Define input/output parameters, function metadata, and execution behavior

### 2. **Plugin Instantiation**
   - Use `NamingPluginFactory.CreatePluginToolAsync()` to create an `IPluginTool` instance
   - The factory handles configuration validation and dependency injection

### 3. **Function Registration**
   - Call `IPluginTool.GetSessionFunctionsAsync()` to retrieve `List<FunctionWithDescription>`
   - Functions are dynamically generated based on the configuration
   - Default function name: `create_subtask` (configurable)

### 4. **Session Creation & Interaction**
   - Create an AI session and register the available functions
   - Send messages and receive responses from the AI assistant
   - The AI can invoke the naming function when task subdivision is needed

### 5. **Function Execution**
   - When invoked, `FunctionWithDescription.Delegate` handles the execution
   - The system validates input parameters against the configuration
   - Child sessions are created based on execution strategy (single or parallel)
   - A message with `Interfaces.MsgBinaryDataType.SubsessionId` is posted to the parent session to provide an entrance for users to access the child session

### 6. **Child Session Management**
   - **Single Execution**: Creates one child session with the provided parameters
   - **Parallel Execution**: Creates multiple child sessions based on configuration type
   
### 7. **Parallel Execution Strategies**

   #### 7.1 **ParameterBased Execution**
   - **Trigger**: `ParallelExecutionConfig.ParallelExecutionType = ParameterBased`
   - **Behavior**: Each parameter in `NamingConfig.InputParameters` (excluding those in `ParallelExecutionConfig.ExcludedParameters`) becomes a separate child session
   - **Template Context**: `_Parameter.Name` = parameter name, `_Parameter.Value` = parameter value
   - **Use Case**: When you want to process different aspects of a task in parallel

   #### 7.2 **ListBased Execution**
   - **Trigger**: `ParallelExecutionConfig.ParallelExecutionType = ListBased`
   - **Requirement**: `ParallelExecutionConfig.ListParameterName` must specify a list/array parameter
   - **Behavior**: Each item in the specified list creates a separate child session
   - **Template Context**: `_Parameter.Name` = `ListParameterName`, `_Parameter.Value` = current list item
   - **Use Case**: Batch processing of similar items (e.g., processing a list of files, users, or tasks)

   #### 7.3 **ExternalList Execution**
   - **Trigger**: `ParallelExecutionConfig.ParallelExecutionType = ExternalList`
   - **Source**: `ParallelExecutionConfig.ExternalList` provides the values
   - **Behavior**: Each string in the external list creates a separate child session
   - **Template Context**: `_Parameter.Name` = "ExternalList", `_Parameter.Value` = current external value
   - **Use Case**: Pre-defined sets of values or scenarios to process

### 8. **Template Rendering & Session Execution**
   - The `PromptMessage` is rendered using Scriban templates with current session context
   - Additional parameters from `InputParameters` are available for template rendering
   - Child sessions receive the rendered prompt and execute independently
   - Results are aggregated based on the configured `ParallelResultStrategy`

## Configuration

### NamingConfig Properties

**Basic Settings**
- `FunctionName`: Custom function name (default: "create_subtask")
- `FunctionDescription`: Function description for AI context
- `MaxRecursionLevel`: Prevents infinite task subdivision (default: 1)

**Template Messages**
- `PromptMessage`: Scriban template for child session prompts
- `UrgingMessage`: Message sent when sessions fail to return results

**Dynamic Parameters**
- `InputParameters`: Configurable function input parameters
- `ReturnParameters`: Expected return value structure

**Assistant Selection**
- `ExecutivePerson`: Specific person/assistant configuration for child sessions

**Parallel Execution**
- `ParallelConfig`: Configuration for concurrent session execution

**Error Handling**
- `DanglingBehavior`: Strategy for handling incomplete sessions (Urge, ReportError, Pause)
- `ErrorMessage`: Custom error message for ReportError behavior
- `ErrorReportingConfig`: Optional error reporting tool configuration

### Parallel Execution Configuration

**Execution Control**
- `ExecutionType`: None, ParameterBased, ListBased, or ExternalList
- `MaxConcurrency`: Concurrent session limit (default: processor count)
- `ResultStrategy`: StreamIndividual, WaitForAll, or FirstResultWins

**Parameter Management**
- `ListParameterName`: For ListBased execution - specifies which parameter contains the list
- `ExternalList`: Pre-configured list of values for ExternalList execution
- `ExcludedParameters`: Parameters to skip during ParameterBased execution

### Error Reporting Configuration

**ErrorReportingConfig Properties**
- `ToolName`: Name of the error reporting tool (default: "report_error")
- `ToolDescription`: Tool description for AI context
- `Parameters`: Configurable tool parameters (e.g., error_message, error_type)
- `Behavior`: ErrorReportingBehavior (Pause or ReportError)
- `CustomErrorMessageToParent`: Custom message returned to parent session

## Parallel Execution

### Execution Types

- **`None`**: Single session execution (default behavior)
- **`ParameterBased`**: Create sessions for each non-excluded parameter
- **`ListBased`**: Create sessions for each item in a specified list parameter
- **`ExternalList`**: Create sessions for each item in a pre-configured list

### Result Strategies

- **`StreamIndividual`**: Stream results as they complete
- **`WaitForAll`**: Wait for all sessions and return combined results
- **`FirstResultWins`**: Return first successful result and cancel others

### Concurrency Control

The `MaxConcurrency` setting limits how many child sessions run simultaneously, preventing resource exhaustion while maintaining performance.

## Error Handling & Recovery

The NamingTool provides comprehensive error handling through two complementary mechanisms that address different failure scenarios:

1. **Dangling Session Recovery**: Handles cases where the LLM fails to call any tool (passive failure)
2. **Error Reporting Tool**: Handles cases where the LLM actively reports errors (active failure reporting)

### Expected Normal Flow

**Success Path**: After completing a task, the LLM is expected to call the **`set_result` tool** to return the results back to the parent session. This is the standard completion mechanism for child sessions.

### Dangling Session Recovery (Passive Error Handling)

**What is a Dangling Session?**
A dangling session occurs when a child session processes input and generates responses but **fails to call any tool** - neither the expected `set_result` tool nor the error reporting tool. This leaves the parent session waiting indefinitely for results.

**When Dangling Behavior Triggers:**
- The LLM completes its processing but doesn't call the expected `set_result` tool
- The LLM encounters issues but doesn't use the error reporting tool  
- The LLM simply stops responding without calling any tool
- The session becomes "stuck" without any tool invocation
- This is a **passive failure** - the system must detect and respond to the lack of action

**DanglingBehavior Strategies:**

- **`Urge` (Default)**: Active intervention with progressive urgency
  - Sends `UrgingMessage` to prompt the assistant to call a tool
  - Retries for a configured number of attempts
  - Reports failure if unsuccessful after all retries
  - Best for: General-purpose scenarios requiring automatic recovery

- **`ReportError`**: Immediate failure reporting with custom messaging
  - Immediately terminates the session upon detection
  - Returns formatted error message to parent using `ErrorMessage`
  - Best for: Batch processing or when fast failure detection is critical

- **`Pause`**: Indefinite waiting for manual intervention
  - Waits indefinitely for any tool to be called
  - Preserves session state for manual correction
  - Best for: Interactive scenarios where human oversight is available

### Error Reporting Tool (Active Error Handling)

**What is the Error Reporting Tool?**
The error reporting tool is an optional tool that enables child sessions to **proactively report errors** when the LLM encounters problems during task execution and chooses to report the error instead of calling the `set_result` tool. This is an **active failure reporting** mechanism.

**When Error Reporting Tool Triggers:**
- The LLM encounters an error or issue during task processing
- The LLM recognizes it cannot complete the task successfully  
- The LLM actively chooses to call the error reporting tool instead of the `set_result` tool
- This is an **active failure** - the LLM deliberately reports the problem rather than attempting to return results

**Error Reporting Tool Configuration**
When `ErrorReportingConfig` is configured, child sessions receive an additional error reporting tool alongside the standard `set_result` tool, allowing them to choose between success (`set_result` tool) and failure (error reporting tool) paths.

**ErrorReportingBehavior Options:**

- **`Pause`**: When error tool is called, pause session execution and wait for manual intervention
  - Preserves full session state and context
  - Allows for interactive problem-solving
  - Ideal for complex scenarios requiring human judgment

- **`ReportError`**: When error tool is called, immediately return error message to parent
  - Uses `CustomErrorMessageToParent` or default error formatting
  - Enables rapid error propagation in automated workflows
  - Suitable for batch processing and automated systems

**Key Benefits:**
- **Proactive Error Detection**: Child sessions can report issues before becoming dangling
- **Structured Error Data**: Configurable parameters (error_message, error_type, severity, etc.)
- **Intelligent Failure Handling**: LLM can choose appropriate failure response instead of calling `set_result`
- **Optional Feature**: Can be disabled by setting `ErrorReportingConfig` to null

### How Both Mechanisms Work Together

**Expected Decision Flow:**
1. **LLM calls `set_result` tool** → Success path, task completed normally with results returned
2. **LLM calls error reporting tool** → `ErrorReportingBehavior` strategy applied (active error handling)
3. **LLM calls neither tool** → `DanglingBehavior` strategy applied (passive error handling)

**Complementary Error Handling:**
- **Error Reporting Tool**: For intelligent LLMs that can recognize and report issues instead of calling `set_result`
- **Dangling Behavior**: Safety net for when LLMs fail to call any tool (including `set_result`)
- **Together**: Provides comprehensive coverage of both active and passive failure scenarios

### Error Message Templates

Both dangling behavior and error reporting support template substitution with variables like `{FunctionName}`, `{SessionId}`, and `{Timestamp}` for contextual error messaging.

## Scriban Template Support

Templates use Scriban's `TemplateContext` and `ScriptObject` for enhanced parameter management and clean separation of concerns:

### Available Objects

- **Input Parameters**: Direct access to configured parameters by name
- **`_Parameter`**: Special object for parallel execution contexts (constructed from tuple parameters)
  - `_Parameter.Name`: Current parameter/list name being processed
  - `_Parameter.Value`: Current parameter/list value being processed

### Cascade Member Access

The NamingTool fully supports **cascade member access** for complex nested object structures and **dictionaries**. You can access deeply nested properties using dot notation in Scriban templates, whether your data comes from:
- **Anonymous objects** created in C# code
- **Dictionary<string, object?>** with nested dictionaries
- **Mixed structures** combining objects and dictionaries
- **Arrays/Lists** of objects or dictionaries


### Template Example

```scriban
Process the following {{ _Parameter.Name }}: {{ _Parameter.Value }} {{_Parameter.Value.itemToBeProcessed}}

Context: {{ background }}
Scope: {{ problemScope }}
Assistant: {{ personName }}

Additional Instructions:
```


## Architecture

### Core Components

- **`NamingPluginFactory`**: Creates and configures plugin instances with dependency injection
- **`NamingHandler`**: Main execution logic and session management coordination
- **`NamingSessionRunner`**: Unified session execution with Scriban template rendering
- **`ParallelSessionManager`**: Handles concurrent session execution with result aggregation
- **`ParallelParameterExtractor`**: Extracts execution sources from configuration parameters
- **`CustomReturnResultTool`**: Handles successful result collection from child sessions (the `set_result` tool)
- **`CustomErrorReportingTool`**: Manages proactive error reporting from child sessions

### Key Configuration Classes

- **`NamingConfig`**: Central configuration with all execution parameters
- **`ParameterConfig`**: Defines function parameter metadata and validation
- **`ParallelExecutionConfig`**: Configures parallel execution behavior and strategies
- **`ErrorReportingConfig`**: Configures optional error reporting tool functionality
- **`ParallelExecutionResult`**: Aggregates results from parallel sessions with status tracking

### Session Lifecycle Management

The architecture supports sophisticated session lifecycle management including tool registration, template rendering, parallel execution coordination, error handling, and resource cleanup. Both the `set_result` tool and error reporting tools are registered dynamically based on configuration.

## Best Practices

### Configuration Design
1. **Parameter Design**: Use descriptive parameter names and appropriate types for clear AI understanding
2. **Template Safety**: Test Scriban templates with sample data to ensure proper rendering
3. **Recursion Limits**: Set appropriate `MaxRecursionLevel` to prevent infinite subdivision loops

### Performance Optimization
4. **Concurrency Tuning**: Adjust `MaxConcurrency` based on system resources and task complexity
5. **Resource Monitoring**: Monitor session resource usage in production environments
6. **Result Strategy Selection**: Choose appropriate `ParallelResultStrategy` for your use case

### Error Handling Strategy Selection
7. **Dangling Behavior Guidelines** (for when LLM fails to call any tool):
   - Use **Urge** for general-purpose scenarios requiring automatic recovery attempts
   - Use **ReportError** for batch processing or when immediate failure detection is needed
   - Use **Pause** for interactive scenarios where manual intervention is available

8. **Error Reporting Tool Configuration** (for when LLM actively reports errors):
   - Enable error reporting (`ErrorReportingConfig`) for complex or long-running tasks where LLMs might encounter issues
   - Use **Pause** behavior for interactive debugging and problem-solving scenarios  
   - Use **ReportError** behavior for automated workflows requiring immediate error propagation
   - Configure meaningful parameter names (error_message, error_type, severity) for structured error data

### Message and Tool Design
9. **Clear Communication**:
   - Design prompts that help AI assistants understand when to call `set_result` vs. error reporting tool
   - Make `set_result` tool requirements clear and simple for AI assistants
   - Provide clear guidance on when to use the error reporting tool vs. `set_result` tool
   - Create clear urging messages that specify exactly what action is needed when no tool is called

10. **Dual Tool Strategy**:
    - Configure error reporting tool parameters that provide useful diagnostic information
    - Ensure `set_result` tool parameters match expected result structure
    - Use consistent parameter naming conventions across both tools
    - Design prompts that guide LLMs on appropriate tool selection (`set_result` for success, error reporting for failures)

### Testing and Validation
11. **Comprehensive Error Handling Testing**:
    - Test **dangling behavior** scenarios (LLM calls no tools, including not calling `set_result`)
    - Test **error reporting tool** scenarios (LLM actively reports errors instead of calling `set_result`)
    - Test **successful completion** scenarios (LLM calls `set_result` tool)
    - Validate all three paths work correctly in your configuration

12. **Error Scenario Testing**:
    - Simulate scenarios where child sessions encounter different types of errors
    - Test manual intervention workflows for both Pause behaviors
    - Verify error message formatting and clarity for both mechanisms
    - Ensure proper cleanup when sessions fail through either path

### Production Deployment
13. **Monitoring and Logging**: Implement comprehensive logging to track both passive failures (dangling) and active failures (error reporting)
14. **Configuration Management**: Store and version control configuration objects for reproducibility
15. **Graceful Degradation**: Design fallback strategies for when parallel execution fails through either error mechanism

