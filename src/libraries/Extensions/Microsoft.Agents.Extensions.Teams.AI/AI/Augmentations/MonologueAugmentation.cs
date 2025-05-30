﻿using Json.More;
using Json.Schema;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Extensions.Teams.AI.Models;
using Microsoft.Agents.Extensions.Teams.AI.Planners;
using Microsoft.Agents.Extensions.Teams.AI.Prompts;
using Microsoft.Agents.Extensions.Teams.AI.Prompts.Sections;
using Microsoft.Agents.Extensions.Teams.AI.Tokenizers;
using Microsoft.Agents.Extensions.Teams.AI.Validators;
using Microsoft.Agents.Extensions.Teams.AI.State;
using Microsoft.Agents.Extensions.Teams.AI.Utilities.JsonConverters;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.Agents.Extensions.Teams.AI.Augmentations
{
    /// <summary>
    /// Inner Monologue
    /// </summary>
    public class InnerMonologue
    {
        private static readonly string[] _requiredInnerMonologue = ["thoughts", "action"];

        /// <summary>
        /// Thoughts
        /// </summary>
        [JsonPropertyName("thoughts")]
        public InnerMonologueThoughts Thoughts { get; set; }

        /// <summary>
        /// Action
        /// </summary>
        [JsonPropertyName("action")]
        public InnerMonologueAction Action { get; set; }

        /// <summary>
        /// Creates an instance of `InnerMonologue`
        /// </summary>
        /// <param name="thoughts">Thoughts</param>
        /// <param name="action">Action</param>
        public InnerMonologue(InnerMonologueThoughts thoughts, InnerMonologueAction action)
        {
            this.Thoughts = thoughts;
            this.Action = action;
        }

        /// <summary>
        /// InnerMonologueThoughts
        /// </summary>
        public class InnerMonologueThoughts
        {
            private static readonly string[] _requiredInnerMonologueThoughts = ["thought", "reasoning"];

        /// <summary>
        /// The LLM's current thought.
        /// </summary>
        [JsonPropertyName("thought")]
            public string Thought { get; set; }

            /// <summary>
            /// The LLM's reasoning for the current thought.
            /// </summary>
            [JsonPropertyName("reasoning")]
            public string Reasoning { get; set; }

            /// <summary>
            /// Creates an instance of `InnerMonologueThoughts`
            /// </summary>
            /// <param name="thought">The LLM's current thought.</param>
            /// <param name="reasoning">The LLM's reasoning for the current thought.</param>
            public InnerMonologueThoughts(string thought, string reasoning)
            {
                this.Thought = thought;
                this.Reasoning = reasoning;
            }

            /// <summary>
            /// Schema
            /// </summary>
            /// <returns></returns>
            public static JsonSchema Schema()
            {
                return new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        (
                            "thought",
                            new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Description("The LLM's current thought.")
                        ),
                        (
                            "reasoning",
                            new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Description("The LLM's reasoning for the current thought.")
                        )
                    )
                    .Required(_requiredInnerMonologueThoughts)
                    .Build();
            }
        }

        /// <summary>
        /// InnerMonologueAction
        /// </summary>
        public class InnerMonologueAction
        {
            private readonly static string[] _requiredInnerMonologueAction = ["name", "parameters"];

            /// <summary>
            /// Name of the action to perform.
            /// </summary>
            [JsonPropertyName("name")]
            public string Name { get; set; }

            /// <summary>
            /// Parameters for the action.
            /// </summary>
            [JsonPropertyName("parameters")]
            [JsonConverter(typeof(DictionaryJsonConverter))]
            public Dictionary<string, object?> Parameters { get; set; }

            /// <summary>
            /// Creates an instance of `InnerMonologueAction`
            /// </summary>
            /// <param name="name">Name of the action to perform.</param>
            /// <param name="parameters">Parameters for the action.</param>
            public InnerMonologueAction(string name, Dictionary<string, object?> parameters)
            {
                this.Name = name;
                this.Parameters = parameters;
            }

            /// <summary>
            /// Schema
            /// </summary>
            /// <returns></returns>
            public static JsonSchema Schema()
            {
                return new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        (
                            "name",
                            new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Description("Name of the action to perform.")
                        ),
                        (
                            "parameters",
                            new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Description("Parameters for the action.")
                        )
                    )
                    .Required(_requiredInnerMonologueAction)
                    .Build();
            }
        }

        /// <summary>
        /// Schema
        /// </summary>
        /// <returns></returns>
        public static JsonSchema Schema()
        {
            return new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    (
                        "thoughts",
                        InnerMonologueThoughts.Schema()
                    ),
                    (
                        "action",
                        InnerMonologueAction.Schema()
                    )
                )
                .Required(_requiredInnerMonologue)
                .Build();
        }
    }

    /// <summary>
    /// Monologue Augmentation
    /// </summary>
    public class MonologueAugmentation : IAugmentation
    {
        private readonly ActionAugmentationSection _section;
        private readonly JsonResponseValidator _monologueValidator;
        private readonly ActionResponseValidator _actionValidator;
        private const string _missingActionFeedback = "The JSON returned had errors. Apply these fixes:\nadd the \"action\" property to \"instance\"";
        private const string _sayRedirectFeedback = "The JSON returned was missing an action. Return a valid JSON object that contains your thoughts and uses the SAY action.";

        private static readonly string[] _requiredMonologueAugmentation = ["text"];
        private static readonly string[] _defaultSectionAppend = new string[]
            {
                "Return a JSON object with your thoughts and the next action to perform.",
                "Only respond with the JSON format below and base your plan on the actions above.",
                "If you're not sure what to do, you can always say something by returning a SAY action.",
                "If you're told your JSON response has errors, do your best to fix them.",
                "Response Format:",
                "{\"thoughts\":{\"thought\":\"<your current thought>\",\"reasoning\":\"<self reflect on why you made this decision>\"},\"action\":{\"name\":\"<action name>\",\"parameters\":{\"<name>\":\"<value>\"}}}"
            };

        /// <summary>
        /// Creates an instance of `MonologueAugmentation`
        /// </summary>
        /// <param name="actions">Actions</param>
        public MonologueAugmentation(List<ChatCompletionAction> actions)
        {
            List<ChatCompletionAction> _actions = new(actions);
            
            _actions.Add(new("SAY")
            {
                Description = "use to ask the user a question or say something",
                Parameters = new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        (
                            "text",
                            new JsonSchemaBuilder()
                                .Type(SchemaValueType.String)
                                .Description("text to say or question to ask")
                        )
                    )
                    .Required(_requiredMonologueAugmentation)
                    .Build()
            });

            this._section = new(_actions, string.Join("\n", _defaultSectionAppend));
            this._monologueValidator = new(InnerMonologue.Schema(), "No valid JSON objects were found in the response. Return a valid JSON object with your thoughts and the next action to perform.");
            this._actionValidator = new(_actions, true);
        }

        /// <inheritdoc />
        public PromptSection? CreatePromptSection()
        {
            return this._section;
        }

        /// <inheritdoc />
        public async Task<Plan?> CreatePlanFromResponseAsync(ITurnContext context, ITurnState memory, PromptResponse response, CancellationToken cancellationToken = default)
        {
            try
            {
                InnerMonologue? monologue = JsonSerializer.Deserialize<InnerMonologue>(response.Message?.GetContent<string>() ?? "");

                if (monologue == null)
                {
                    return await Task.FromResult<Plan?>(null);
                }

                IPredictedCommand command;

                if (monologue.Action.Name == "SAY")
                {
                    string text = "";

                    if (monologue.Action.Parameters?.ContainsKey("text") == true)
                    {
                        object? value = monologue.Action.Parameters["text"];

                        if (value != null)
                        {
#pragma warning disable CS8600 // Converting null literal or possible null value to non-nullable type.
                            text = value.ToString();
#pragma warning restore CS8600 // Converting null literal or possible null value to non-nullable type.
                        }
                    }

                    ChatMessage message = response.Message ?? new ChatMessage(ChatRole.Assistant)
                    {
                        Context = response.Message?.Context,
                    };

                    message.Content = text;
                    command = new PredictedSayCommand(message);
                }
                else
                {
                    command = new PredictedDoCommand(monologue.Action.Name, monologue.Action.Parameters);
                }

                return await Task.FromResult(new Plan()
                {
                    Commands =
                    {
                        command
                    }
                });
            }
            catch (Exception)
            {
                return await Task.FromResult<Plan?>(null);
            }
        }

        /// <inheritdoc />
        public async Task<Validation> ValidateResponseAsync(ITurnContext context, ITurnState memory, ITokenizer tokenizer, PromptResponse response, int remainingAttempts, CancellationToken cancellationToken = default)
        {
            Validation validation = await this._monologueValidator.ValidateResponseAsync(context, memory, tokenizer, response, remainingAttempts, cancellationToken);

            if (!validation.Valid)
            {
                string? feedback = validation.Feedback;

                if (feedback == _missingActionFeedback)
                {
                    feedback = _sayRedirectFeedback;
                }

                return new()
                {
                    Valid = false,
                    Feedback = feedback
                };
            }

            InnerMonologue? monologue = ((Dictionary<string, JsonElement>?)validation.Value)?.AsJsonElement().Deserialize<InnerMonologue>();

            if (monologue == null)
            {
                return new()
                {
                    Valid = false,
                    Feedback = "monologue response could not be deserialized"
                };
            }

            string parameters = JsonSerializer.Serialize(monologue.Action.Parameters);
            PromptResponse promptResponse = new()
            {
                Status = PromptResponseStatus.Success,
                Message = new(ChatRole.Assistant)
                {
                    FunctionCall = new(monologue.Action.Name, parameters)
                }
            };

            Validation valid = await this._actionValidator.ValidateResponseAsync(context, memory, tokenizer, promptResponse, remainingAttempts, cancellationToken);

            if (!valid.Valid)
            {
                return new()
                {
                    Valid = false,
                    Feedback = valid.Feedback
                };
            }

            return new()
            {
                Valid = true,
                Value = JsonSerializer.Serialize(monologue)
            };
        }
    }
}
