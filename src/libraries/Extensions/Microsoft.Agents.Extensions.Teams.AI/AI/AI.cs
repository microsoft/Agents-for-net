﻿using Microsoft.Agents.Extensions.Teams.AI.Action;
using Microsoft.Agents.Extensions.Teams.AI.Planners;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.Extensions.Teams.AI.Moderator;
using Microsoft.Agents.Extensions.Teams.AI.Utilities;
using Microsoft.Agents.Builder;
using Microsoft.Agents.Builder.State;
using Microsoft.Agents.Builder.App;

namespace Microsoft.Agents.Extensions.Teams.AI
{
    /// <summary>
    /// AI System.
    /// </summary>
    /// <remarks>
    /// The AI system is responsible for generating plans, moderating input and output, and
    /// generating prompts. It can be used free standing or routed to by the Application{TState} object.
    /// </remarks>
    //// <typeparam name="TState">Optional. Type of the turn state.</typeparam>
    public class AISystem
    {
        private readonly ActionCollection<ITurnState> _actions;

        /// <summary>
        /// Creates an instance of the AISystem{TState} class.
        /// </summary>
        /// <param name="options">The options to configure.</param>
        /// <param name="loggerFactory">Optional. The logger factory to use.</param>
        public AISystem(AIOptions options, ILoggerFactory? loggerFactory = null)
        {
            Verify.ParamNotNull(options);

            Options = new AIOptions(options.Planner)
            {
                Moderator = options.Moderator ?? new DefaultModerator<ITurnState>(),
                MaxSteps = options.MaxSteps ?? 25,
                MaxTime = options.MaxTime ?? TimeSpan.FromMilliseconds(300000),
                AllowLooping = options.AllowLooping ?? true,
                EnableFeedbackLoop = options.EnableFeedbackLoop,
            };
            _actions = new ActionCollection<ITurnState>();

            // Import default actions
            ImportActions(new DefaultActions<ITurnState>(options.EnableFeedbackLoop, options.FeedbackLoopType, loggerFactory));
        }

        /// <summary>
        /// Returns the moderator being used by the AI system.
        /// </summary>
        /// <remarks>
        /// The default moderator simply allows all messages and plans through without intercepting them.
        /// </remarks>
        public IModerator<ITurnState> Moderator => Options.Moderator!;

        /// <summary>
        /// Returns the options for the AI system.
        /// </summary>
        public AIOptions Options { get; }

        /// <summary>
        /// Returns the planner being used by the AI system.
        /// </summary>
        public IPlanner<ITurnState> Planner => Options.Planner;

        /// <summary>
        /// Registers a handler for a named action.
        /// </summary>
        /// <remarks>
        /// The AI system's planner returns plans that are made up of a series of commands or actions
        /// that should be performed. Registering a handler lets you provide code that should be run in
        /// response to one of the predicted actions.
        /// 
        /// Plans support a DO command which specifies the name of an action to call and an optional
        /// set of entities that should be passed to the action. The internal plan executor will call
        /// the registered handler for the action passing in the current context, state, and entities.
        /// 
        /// Additionally, the AI system itself uses actions to handle things like unknown actions,
        /// flagged input, and flagged output. You can override these actions by registering your own
        /// handler for them. The names of the built-in actions are available as static properties on
        /// the AI class.
        /// </remarks>
        /// <param name="name">The name of the action.</param>
        /// <param name="handler">The action handler function.</param>
        /// <returns>The current instance object.</returns>
        /// <exception cref="System.InvalidOperationException"></exception>
        public AISystem RegisterAction(string name, IActionHandler<ITurnState> handler)
        {
            Verify.ParamNotNull(name);
            Verify.ParamNotNull(handler);

            if (!_actions.ContainsAction(name))
            {
                _actions.AddAction(name, handler, allowOverrides: false);
            }
            else
            {
                ActionEntry<ITurnState> entry = _actions[name];
                if (entry.AllowOverrides)
                {
                    entry.Handler = handler;
                    entry.AllowOverrides = false; // Only override once
                }
                else
                {
                    throw new InvalidOperationException($"Attempting to register an already existing action `{name}` that does not allow overrides.");
                }
            }

            return this;
        }

        /// <summary>
        /// Registers the default handler for a named action.
        /// </summary>
        /// <remarks>
        /// Default handlers can be replaced by calling the RegisterAction(string, ActionHandler{TState}) method with the same name.
        /// </remarks>
        /// <param name="name">The name of the action.</param>
        /// <param name="handler">The action handler function.</param>
        /// <returns>The current instance object.</returns>
        public AISystem RegisterDefaultAction(string name, IActionHandler<ITurnState> handler)
        {
            Verify.ParamNotNull(name);
            Verify.ParamNotNull(handler);

            if (!_actions.ContainsAction(name))
            {
                _actions.AddAction(name, handler, allowOverrides: true);
            }
            else
            {
                ActionEntry<ITurnState> entry = _actions[name];
                entry.Handler = handler;
                entry.AllowOverrides = true;
            }

            return this;
        }

        /// <summary>
        /// Import a set of Actions from the given class instance. The functions must have the `Action` attribute.
        /// Once these functions are imported, the AI System will have access to these functions.
        /// </summary>
        /// <param name="instance">Instance of a class containing these functions.</param>
        /// <returns>The current instance object.</returns>
        /// <exception cref="System.InvalidOperationException"></exception>
        public AISystem ImportActions(object instance)
        {
            Verify.ParamNotNull(instance);

            MethodInfo[] methods = instance.GetType()
                .GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod);

            // Filter out null functions
            IEnumerable<ActionEntry<ITurnState>> functions = from method in methods select ActionEntry<ITurnState>.FromNativeMethod(method, instance);
            List<ActionEntry<ITurnState>> result = (from function in functions where function != null select function).ToList();

            // Fail if two functions have the same name
            HashSet<string> uniquenessCheck = new(from x in result select x.Name, StringComparer.OrdinalIgnoreCase);
            if (result.Count > uniquenessCheck.Count)
            {
                throw new InvalidOperationException("Function overloads are not supported, please differentiate function names");
            }

            // Register the actions
            result.ForEach(action =>
            {
                if (action.AllowOverrides)
                {
                    RegisterDefaultAction(action.Name, action.Handler);
                }
                else
                {
                    RegisterAction(action.Name, action.Handler);
                }
            });

            return this;
        }

        /// <summary>
        /// Checks to see if the AI system has a handler for a given action.
        /// </summary>
        /// <param name="action">Name of the action to check.</param>
        /// <returns>True if the AI system has a handler for the given action.</returns>
        public bool ContainsAction(string action)
        {
            return _actions.ContainsAction(action);
        }

        /// <summary>
        /// Calls the configured planner to generate a plan and executes the plan that is returned.
        /// </summary>
        /// <remarks>
        /// The moderator is called to review the input and output of the plan. If the moderator flags
        /// the input or output then the appropriate action is called. If the moderator allows the input
        /// and output then the plan is executed.
        /// </remarks>
        /// <param name="turnContext">Current turn context.</param>
        /// <param name="turnState">Current turn state.</param>
        /// <param name="startTime">Optional. Time the AI system started running.</param>
        /// <param name="stepCount">Number of steps that have been executed.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>True if the plan was completely executed, otherwise false.</returns>
        public async Task<bool> RunAsync(ITurnContext turnContext, ITurnState turnState, DateTime? startTime = null, int stepCount = 0, CancellationToken cancellationToken = default)
        {
            Verify.ParamNotNull(turnContext);
            Verify.ParamNotNull(turnState);

            // Initialize start time
            startTime = startTime ?? DateTime.UtcNow;
            Plan? plan = null;

            // Review input on first loop
            if (stepCount == 0)
            {
                plan = await Options.Moderator!.ReviewInputAsync(turnContext, turnState, cancellationToken);
            }

            // Generate plan
            if (plan == null)
            {
                if (stepCount == 0)
                {
                    plan = await Options.Planner.BeginTaskAsync(turnContext, turnState, this, cancellationToken);
                }
                else
                {
                    plan = await Options.Planner.ContinueTaskAsync(turnContext, turnState, this, cancellationToken);
                }

                // Review the plans output
                plan = await Options.Moderator!.ReviewOutputAsync(turnContext, turnState, plan, cancellationToken);
            }

            // Process generated plan
            string response = await _actions[AIConstants.PlanReadyActionName].Handler.PerformActionAsync(turnContext, turnState, plan, AIConstants.PlanReadyActionName, cancellationToken);
            if (string.Equals(response, AIConstants.StopCommand))
            {
                return false;
            }

            // Run predicted commands
            // - If the plan ends on a SAY command then the plan is considered complete, otherwise we'll loop
            bool completed = true;
            bool shouldLoop = false;
            foreach (IPredictedCommand command in plan.Commands)
            {
                // Check for timeout
                if (DateTime.UtcNow - startTime > Options.MaxTime || ++stepCount > Options.MaxSteps)
                {
                    completed = false;
                    TooManyStepsParameters parameters = new(Options.MaxSteps!.Value, Options.MaxTime!.Value, startTime.Value, stepCount);
                    await _actions[AIConstants.TooManyStepsActionName]
                        .Handler
                        .PerformActionAsync(turnContext, turnState, parameters, AIConstants.TooManyStepsActionName, cancellationToken);
                    break;
                }

                string output;
                PredictedDoCommand? doCommand = null;
                if (command is PredictedDoCommand)
                {
                    doCommand = (PredictedDoCommand)command;
                    if (_actions.ContainsAction(doCommand.Action))
                    {
                        DoCommandActionData<ITurnState> data = new()
                        {
                            PredictedDoCommand = doCommand,
                            Handler = _actions[doCommand.Action].Handler
                        };

                        // Call action handler
                        output = await this._actions[AIConstants.DoCommandActionName]
                            .Handler
                            .PerformActionAsync(turnContext, turnState, data, doCommand.Action, cancellationToken);

                        Dictionary<string, string> actionOutputs = turnState.Temp!.GetValue("actionOutputs", () => new Dictionary<string, string>());

                        if (doCommand.ActionId != null)
                        {
                            shouldLoop = true;
                            actionOutputs[doCommand.ActionId] = output;
                        }
                        else
                        {
                            shouldLoop = output.Length > 0;
                            actionOutputs[doCommand.Action] = output;
                        }
                    }
                    else
                    {
                        // Redirect to UnknownAction handler
                        output = await this._actions[AIConstants.UnknownActionName]
                            .Handler
                            .PerformActionAsync(turnContext, turnState, plan, doCommand.Action, cancellationToken);
                    }
                }
                else if (command is PredictedSayCommand sayCommand)
                {
                    shouldLoop = false;
                    output = await this._actions[AIConstants.SayCommandActionName]
                        .Handler
                        .PerformActionAsync(turnContext, turnState, sayCommand, AIConstants.SayCommandActionName, cancellationToken);
                }
                else
                {
                    throw new InvalidOperationException($"Unknown command of {command.Type} predicted");
                }

                // Check for stop command
                if (string.Equals(output, AIConstants.StopCommand))
                {
                    completed = false;
                    break;
                }

                // Copy the actions output to the input
                turnState.Temp.SetValue("inputFiles", new List<InputFile>());

                if (doCommand != null && doCommand.ActionId != null)
                {
                    turnState.DeleteValue("temp.input");
                }
                else
                {
                    turnState.Temp!.SetValue("input", output);
                }
            }

            // Check for looping
            if (completed && shouldLoop && Options.AllowLooping!.Value)
            {
                return await RunAsync(turnContext, turnState, startTime, stepCount, cancellationToken);
            }
            else
            {
                return completed;
            }
        }

#pragma warning disable CA1822 // Mark members as static
        private void _SetTempStateValues(ITurnState turnState, ITurnContext turnContext)
#pragma warning restore CA1822 // Mark members as static
        {
            TempState? tempState = turnState.Temp;

            if (tempState != null)
            {
                string input = tempState.GetValue<string>("input");
                if (string.IsNullOrEmpty(input))
                {
                    tempState.SetValue("input", turnContext.Activity.Text ?? string.Empty);
                }
            }
        }
    }
}
