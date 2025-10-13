using Naming;
using Naming.ParallelExecution;
using Scriban;
using Scriban.Runtime;
using System;
using System.Collections.Generic;

namespace TestNamingTool.UnitTests.Templates
{
    public class ScribanTemplateTests
    {
        [Fact]
        public void ScribanTemplate_WithSimpleVariables_RendersCorrectly()
        {
            // Arrange
            var template = Template.Parse("Hello {{ name }}, you are {{ age }} years old");
            var context = new TemplateContext();
            var scriptObject = new ScriptObject();
            scriptObject["name"] = "John";
            scriptObject["age"] = 25;
            context.PushGlobal(scriptObject);

            // Act
            var result = template.Render(context);

            // Assert
            result.Should().Be("Hello John, you are 25 years old");
        }

        [Fact]
        public void ScribanTemplate_WithParameterObject_RendersParameterInfo()
        {
            // Arrange
            var template = Template.Parse("Processing {{ _Parameter.Name }}: {{ _Parameter.Value }}");
            var context = new TemplateContext();
            var scriptObject = new ScriptObject();
            
            var parameterObj = new ScriptObject();
            parameterObj["Name"] = "testParam";
            parameterObj["Value"] = "testValue";
            scriptObject["_Parameter"] = parameterObj;
            
            context.PushGlobal(scriptObject);

            // Act
            var result = template.Render(context);

            // Assert
            result.Should().Be("Processing testParam: testValue");
        }


        [Fact]
        public void ScribanTemplate_WithConditionalLogic_RendersConditionally()
        {
            // Arrange
            var template = Template.Parse("{{ if hasData }}Data: {{ data }}{{ else }}No data available{{ end }}");
            var context = new TemplateContext();
            var scriptObject = new ScriptObject();
            scriptObject["hasData"] = true;
            scriptObject["data"] = "sample data";
            context.PushGlobal(scriptObject);

            // Act
            var result = template.Render(context);

            // Assert
            result.Should().Be("Data: sample data");
        }

        [Fact]
        public void ScribanTemplate_WithConditionalLogic_RendersElseBranch()
        {
            // Arrange
            var template = Template.Parse("{{ if hasData }}Data: {{ data }}{{ else }}No data available{{ end }}");
            var context = new TemplateContext();
            var scriptObject = new ScriptObject();
            scriptObject["hasData"] = false;
            context.PushGlobal(scriptObject);

            // Act
            var result = template.Render(context);

            // Assert
            result.Should().Be("No data available");
        }

        [Fact]
        public void ScribanTemplate_WithLoop_RendersMultipleItems()
        {
            // Arrange
            var template = Template.Parse("Items: {{ for item in items }}{{ item }}{{ if !for.last }}, {{ end }}{{ end }}");
            var context = new TemplateContext();
            var scriptObject = new ScriptObject();
            scriptObject["items"] = new[] { "apple", "banana", "cherry" };
            context.PushGlobal(scriptObject);

            // Act
            var result = template.Render(context);

            // Assert
            result.Should().Be("Items: apple, banana, cherry");
        }

        [Fact]
        public void ScribanTemplate_WithMissingVariable_RendersEmpty()
        {
            // Arrange
            var template = Template.Parse("Value: {{ missingVariable }}");
            var context = new TemplateContext();
            var scriptObject = new ScriptObject();
            context.PushGlobal(scriptObject);

            // Act
            var result = template.Render(context);

            // Assert
            result.Should().Be("Value: ");
        }

        [Fact]
        public void ScribanTemplate_WithNullValue_RendersEmpty()
        {
            // Arrange
            var template = Template.Parse("Value: {{ nullValue }}");
            var context = new TemplateContext();
            var scriptObject = new ScriptObject();
            scriptObject["nullValue"] = null;
            context.PushGlobal(scriptObject);

            // Act
            var result = template.Render(context);

            // Assert
            result.Should().Be("Value: ");
        }



        [Fact]
        public void ScribanTemplate_WithStringFunctions_AppliesFunctions()
        {
            // Arrange
            var template = Template.Parse("Upper: {{ name | string.upcase }}, Lower: {{ name | string.downcase }}");
            var context = new TemplateContext();
            var scriptObject = new ScriptObject();
            scriptObject["name"] = "John Doe";
            context.PushGlobal(scriptObject);

            // Act
            var result = template.Render(context);

            // Assert
            result.Should().Be("Upper: JOHN DOE, Lower: john doe");
        }

        [Fact]
        public void ScribanTemplate_WithDateFormatting_FormatsCorrectly()
        {
            // Arrange
            var template = Template.Parse("Date: {{ input_date | date.to_string '%Y-%m-%d' }}");
            var context = new TemplateContext();
            var scriptObject = new ScriptObject();
            scriptObject["input_date"] = new DateTime(2023, 12, 25);
            context.PushGlobal(scriptObject);

            // Act
            var result = template.Render(context);

            // Assert
            result.Should().Be("Date: 2023-12-25");
        }

        [Fact]
        public void ScribanTemplate_WithArrayAccess_AccessesElements()
        {
            // Arrange
            var template = Template.Parse("First: {{ items[0] }}, Second: {{ items[1] }}");
            var context = new TemplateContext();
            var scriptObject = new ScriptObject();
            scriptObject["items"] = new[] { "apple", "banana", "cherry" };
            context.PushGlobal(scriptObject);

            // Act
            var result = template.Render(context);

            // Assert
            result.Should().Be("First: apple, Second: banana");
        }

        [Fact]
        public void ScribanTemplate_WithMathOperations_CalculatesCorrectly()
        {
            // Arrange
            var template = Template.Parse("Sum: {{ a + b }}, Product: {{ a * b }}");
            var context = new TemplateContext();
            var scriptObject = new ScriptObject();
            scriptObject["a"] = 5;
            scriptObject["b"] = 3;
            context.PushGlobal(scriptObject);

            // Act
            var result = template.Render(context);

            // Assert
            result.Should().Be("Sum: 8, Product: 15");
        }


        [Theory]
        [InlineData("{{ name }}", "John", "John")]
        [InlineData("Hello {{ name }}!", "World", "Hello World!")]
        [InlineData("{{ if true }}Yes{{ else }}No{{ end }}", null, "Yes")]
        [InlineData("{{ if false }}Yes{{ else }}No{{ end }}", null, "No")]
        public void ScribanTemplate_WithVariousTemplates_RendersExpectedResults(string templateText, string? nameValue, string expected)
        {
            // Arrange
            var template = Template.Parse(templateText);
            var context = new TemplateContext();
            var scriptObject = new ScriptObject();
            if (nameValue != null)
            {
                scriptObject["name"] = nameValue;
            }
            context.PushGlobal(scriptObject);

            // Act
            var result = template.Render(context);

            // Assert
            result.Should().Be(expected);
        }
    }
}
