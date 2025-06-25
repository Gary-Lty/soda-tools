using Stateless.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Stateless
{
    /// <summary>
    /// Enum for the different modes used when <c>Fire</c>ing a trigger
    /// </summary>
    public enum FiringMode
    {
        /// <summary>
        /// Use immediate mode when the queuing of trigger events are not needed. Care must be taken when using this mode, as there is no run-to-completion guaranteed.
        /// 当不需要触发事件排队时，使用立即模式。使用此模式时必须小心，因为没有运行到完成的保证。
        /// </summary>
        Immediate,
        /// <summary> 
        /// Use the queued <c>Fire</c>ing mode when run-to-completion is required. This is the recommended mode.
        /// 当需要运行到完成时，使用queued <c>Fire</c>模式。推荐使用该模式。
        /// </summary>
        Queued
    }

    /// <summary>
    /// Models behaviour as transitions between a finite set of states.
    /// 将行为建模为一组有限状态之间的转换。
    /// </summary>
    /// <typeparam name="TState">The type used to represent the states.</typeparam>
    /// <typeparam name="TTrigger">The type used to represent the triggers that cause state transitions.</typeparam>
    public partial class StateMachine<TState, TTrigger>
    {
        private readonly IDictionary<TState, StateRepresentation> _stateConfiguration = new Dictionary<TState, StateRepresentation>();
        private readonly IDictionary<TTrigger, TriggerWithParameters> _triggerConfiguration = new Dictionary<TTrigger, TriggerWithParameters>();
        private readonly Func<TState> _stateAccessor;
        private readonly Action<TState> _stateMutator;
        private UnhandledTriggerAction _unhandledTriggerAction;
        private readonly OnTransitionedEvent _onTransitionedEvent;
        private readonly OnTransitionedEvent _onTransitionCompletedEvent;
        private readonly TState _initialState;
        private readonly FiringMode _firingMode;

        private class QueuedTrigger
        {
            public TTrigger Trigger { get; set; }
            public object[] Args { get; set; }
        }

        private readonly Queue<QueuedTrigger> _eventQueue = new Queue<QueuedTrigger>();
        private bool _firing;

        /// <summary>
        /// Construct a state machine with external state storage.
        /// 构造一个带有外部状态存储的状态机。
        /// </summary>
        /// <param name="stateAccessor">A function that will be called to read the current state value.</param>
        /// <param name="stateMutator">An action that will be called to write new state values.</param>
        public StateMachine(Func<TState> stateAccessor, Action<TState> stateMutator) : this(stateAccessor, stateMutator, FiringMode.Queued)
        {
        }

        /// <summary>
        /// Construct a state machine.
        /// </summary>
        /// <param name="initialState">The initial state.</param>
        public StateMachine(TState initialState) : this(initialState, FiringMode.Queued)
        {
        }

        /// <summary>
        /// Construct a state machine with external state storage.
        /// 构造一个带有外部状态存储的状态机。
        /// </summary>
        /// <param name="stateAccessor">A function that will be called to read the current state value.用于读取当前状态值的函数。</param>
        /// <param name="stateMutator">An action that will be called to write new state values.用于写入新状态值的操作。</param>
        /// <param name="firingMode">Optional specification of firing mode.</param>
        public StateMachine(Func<TState> stateAccessor, Action<TState> stateMutator, FiringMode firingMode) : this()
        {
            _stateAccessor = stateAccessor ?? throw new ArgumentNullException(nameof(stateAccessor));
            _stateMutator = stateMutator ?? throw new ArgumentNullException(nameof(stateMutator));

            _initialState = stateAccessor();
            _firingMode = firingMode;
        }

        /// <summary>
        /// Construct a state machine.
        /// 构造一个状态机。
        /// </summary>
        /// <param name="initialState">The initial state.</param>
        /// <param name="firingMode">Optional specification of firing mode.</param>
        public StateMachine(TState initialState, FiringMode firingMode) : this()
        {
            var reference = new StateReference { State = initialState };
            _stateAccessor = () => reference.State;
            _stateMutator = s => reference.State = s;

            _initialState = initialState;
            _firingMode = firingMode;
        }

        /// <summary>
        /// For certain situations, it is essential that the SynchronizationContext is retained for all delegate calls.
        /// 在某些情况下，必须为所有委托调用保留SynchronizationContext。
        /// </summary>
        public bool RetainSynchronizationContext { get; set; } = false;

        /// <summary>
        /// Default constructor
        /// 默认构造器
        /// </summary>
        StateMachine()
        {
            _unhandledTriggerAction = new UnhandledTriggerAction.Sync(DefaultUnhandledTriggerAction);
            _onTransitionedEvent = new OnTransitionedEvent();
            _onTransitionCompletedEvent = new OnTransitionedEvent();
        }

        /// <summary>
        /// The current state.
        /// 当前状态
        /// </summary>
        public TState State
        {
            get
            {
                return _stateAccessor();
            }
            private set
            {
                _stateMutator(value);
            }
        }

        /// <summary>
        /// The currently-permissible trigger values.
        /// 当前设定的允许的触发值。
        /// </summary>
        public IEnumerable<TTrigger> PermittedTriggers
        {
            get
            {
                return GetPermittedTriggers();
            }
        }

        /// <summary>
        /// The currently-permissible trigger values.
        /// 当前设定的允许的触发值。
        /// </summary>
        public IEnumerable<TTrigger> GetPermittedTriggers(params object[] args)
        {
            return CurrentRepresentation.GetPermittedTriggers(args);
        }

        /// <summary>
        /// Gets the currently-permissible triggers with any configured parameters.
        /// 获取具有任何已配置参数的当前允许的触发器。
        /// </summary>
        public IEnumerable<TriggerDetails<TState, TTrigger>> GetDetailedPermittedTriggers(params object[] args)
        {
            return CurrentRepresentation.GetPermittedTriggers(args)
                .Select(trigger => new TriggerDetails<TState, TTrigger>(trigger, _triggerConfiguration));
        }

        StateRepresentation CurrentRepresentation
        {
            get
            {
                return GetRepresentation(State);
            }
        }

        /// <summary>
        /// Provides an info object which exposes the states, transitions, and actions of this machine.
        /// 提供一个info对象，该对象公开此机器的状态、转换和操作。
        /// </summary>
        public StateMachineInfo GetInfo()
        {
            var initialState = StateInfo.CreateStateInfo(new StateRepresentation(_initialState, RetainSynchronizationContext));

            var representations = _stateConfiguration.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            var behaviours = _stateConfiguration.SelectMany(kvp => kvp.Value.TriggerBehaviours.SelectMany(b => b.Value.OfType<TransitioningTriggerBehaviour>().Select(tb => tb.Destination))).ToList();
            behaviours.AddRange(_stateConfiguration.SelectMany(kvp => kvp.Value.TriggerBehaviours.SelectMany(b => b.Value.OfType<ReentryTriggerBehaviour>().Select(tb => tb.Destination))).ToList());

            var reachable = behaviours
                .Distinct()
                .Except(representations.Keys)
                .Select(underlying => new StateRepresentation(underlying, RetainSynchronizationContext))
                .ToArray();

            foreach (var representation in reachable)
                representations.Add(representation.UnderlyingState, representation);

            var info = representations.ToDictionary(kvp => kvp.Key, kvp => StateInfo.CreateStateInfo(kvp.Value));

            foreach (var state in info)
                StateInfo.AddRelationships(state.Value, representations[state.Key], k => info[k]);

            return new StateMachineInfo(info.Values, typeof(TState), typeof(TTrigger), initialState);
        }

        StateRepresentation GetRepresentation(TState state)
        {
            if (!_stateConfiguration.TryGetValue(state, out StateRepresentation result))
            {
                result = new StateRepresentation(state, RetainSynchronizationContext);
                _stateConfiguration.Add(state, result);
            }

            return result;
        }

        /// <summary>
        /// Begin configuration of the entry/exit actions and allowed transitions
        /// when the state machine is in a particular state.
        /// 当状态机处于特定状态时开始配置进入/退出动作和允许的转换。
        /// </summary>
        /// <param name="state">The state to configure.</param>
        /// <returns>A configuration object through which the state can be configured.
        /// 一个配置对象，通过它可以配置状态
        /// </returns>
        public StateConfiguration Configure(TState state)
        {
            return new StateConfiguration(this, GetRepresentation(state), GetRepresentation);
        }

        /// <summary>
        /// Transition from the current state via the specified trigger.
        /// 通过指定的触发器从当前状态进行转换。
        /// The target state is determined by the configuration of the current state.
        /// 目标状态由当前状态的配置决定。
        /// Actions associated with leaving the current state and entering the new one will be invoked.
        /// 将调用与离开当前状态并进入新状态相关联的操作。
        /// </summary>
        /// <param name="trigger">The trigger to fire.</param>
        /// <exception cref="System.InvalidOperationException">The current state does
        /// not allow the trigger to be fired.</exception>
        public void Fire(TTrigger trigger)
        {
            InternalFire(trigger, new object[0]);
        }

        /// <summary>
        /// Transition from the current state via the specified trigger.
        /// 通过指定的触发器从当前状态进行转换。
        /// The target state is determined by the configuration of the current state.
        /// 目标状态由当前状态的配置决定。
        /// Actions associated with leaving the current state and entering the new one will be invoked.
        /// 将调用与离开当前状态并进入新状态相关联的操作。
        /// </summary>
        /// <param name="trigger">The trigger to fire.</param>
        /// <param name="args">A variable-length parameters list containing arguments. 包含实参的可变长度形参列表。</param>
        /// <exception cref="System.InvalidOperationException">The current state does
        /// not allow the trigger to be fired.</exception>
        public void Fire(TriggerWithParameters trigger, params object[] args)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            InternalFire(trigger.Trigger, args);
        }

        /// <summary>
        /// Specify the arguments that must be supplied when a specific trigger is fired.
        /// 指定在触发特定触发器时必须提供的参数。
        /// </summary>
        /// <param name="trigger">The underlying trigger value.</param>
        /// <param name="argumentTypes">The argument types expected by the trigger.</param>
        /// <returns>An object that can be passed to the Fire() method in order to
        /// fire the parameterised trigger.</returns>
        public TriggerWithParameters SetTriggerParameters(TTrigger trigger, params Type[] argumentTypes)
        {
            var configuration = new TriggerWithParameters(trigger, argumentTypes);
            SaveTriggerConfiguration(configuration);
            return configuration;
        }

        /// <summary>
        /// Transition from the current state via the specified trigger.
        /// 通过指定的触发器从当前状态进行转换。
        /// The target state is determined by the configuration of the current state.
        /// 目标状态由当前状态的配置决定。
        /// Actions associated with leaving the current state and entering the new one will be invoked.
        /// 将调用与离开当前状态并进入新状态相关联的操作。
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <param name="trigger">The trigger to fire.</param>
        /// <param name="arg0">The first argument.</param>
        /// <exception cref="System.InvalidOperationException">The current state does
        /// not allow the trigger to be fired.</exception>
        public void Fire<TArg0>(TriggerWithParameters<TArg0> trigger, TArg0 arg0)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            InternalFire(trigger.Trigger, arg0);
        }

        /// <summary>
        /// Transition from the current state via the specified trigger.
        /// 通过指定的触发器从当前状态进行转换。
        /// The target state is determined by the configuration of the current state.
        /// 目标状态由当前状态的配置决定。
        /// Actions associated with leaving the current state and entering the new one will be invoked.
        /// 将调用与离开当前状态并进入新状态相关联的操作。
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <typeparam name="TArg1">Type of the second trigger argument.</typeparam>
        /// <param name="arg0">The first argument.</param>
        /// <param name="arg1">The second argument.</param>
        /// <param name="trigger">The trigger to fire.</param>
        /// <exception cref="System.InvalidOperationException">The current state does
        /// not allow the trigger to be fired.</exception>
        public void Fire<TArg0, TArg1>(TriggerWithParameters<TArg0, TArg1> trigger, TArg0 arg0, TArg1 arg1)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            InternalFire(trigger.Trigger, arg0, arg1);
        }

        /// <summary>
        /// Transition from the current state via the specified trigger.
        /// 通过指定的触发器从当前状态进行转换。
        /// The target state is determined by the configuration of the current state.
        /// 目标状态由当前状态的配置决定。
        /// Actions associated with leaving the current state and entering the new one will be invoked.
        /// 将调用与离开当前状态并进入新状态相关联的操作。
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <typeparam name="TArg1">Type of the second trigger argument.</typeparam>
        /// <typeparam name="TArg2">Type of the third trigger argument.</typeparam>
        /// <param name="arg0">The first argument.</param>
        /// <param name="arg1">The second argument.</param>
        /// <param name="arg2">The third argument.</param>
        /// <param name="trigger">The trigger to fire.</param>
        /// <exception cref="System.InvalidOperationException">The current state does
        /// not allow the trigger to be fired.</exception>
        public void Fire<TArg0, TArg1, TArg2>(TriggerWithParameters<TArg0, TArg1, TArg2> trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));
            InternalFire(trigger.Trigger, arg0, arg1, arg2);
        }

        /// <summary>
        /// Activates current state. Actions associated with activating the current state will be invoked. 
        /// 与激活当前状态相关的操作将被调用。
        /// The activation is idempotent and subsequent activation of the same current state will not lead to re-execution of activation callbacks.
        /// 此激活操作是幂等的，对同一当前状态的后续激活不会导致激活回调的重复执行。
        /// </summary>
        public void Activate()
        {
            var representativeState = GetRepresentation(State);
            representativeState.Activate();
        }

        /// <summary>
        /// Deactivates current state. Actions associated with deactivating the current state will be invoked.
        /// 与停用当前状态相关的操作将被调用。
        /// The deactivation is idempotent and subsequent deactivation of the same current state will not lead to re-execution of deactivation callbacks.
        /// 此停用操作是幂等的，对同一当前状态的后续停用不会导致停用回调的重复执行。
        /// </summary>
        public void Deactivate()
        {
            var representativeState = GetRepresentation(State);
            representativeState.Deactivate();
        }

        /// <summary>
        /// Determine how to Fire the trigger
        /// 确定如何触发触发器
        /// </summary>
        /// <param name="trigger">The trigger. </param>
        /// <param name="args">A variable-length parameters list containing arguments. </param>
        void InternalFire(TTrigger trigger, params object[] args)
        {
            switch (_firingMode)
            {
                case FiringMode.Immediate:
                    InternalFireOne(trigger, args);
                    break;
                case FiringMode.Queued:
                    InternalFireQueued(trigger, args);
                    break;
                default:
                    // If something is completely messed up we let the user know ;-)
                    throw new InvalidOperationException("The firing mode has not been configured!");
            }
        }

        /// <summary>
        /// Queue events and then fire in order.
        /// 将事件排入队列，然后按顺序触发。
        /// If only one event is queued, this behaves identically to the non-queued version.
        /// 如果队列中只有一个事件，其行为与非队列版本完全相同。
        /// </summary>
        /// <param name="trigger">  The trigger. </param>
        /// <param name="args">     A variable-length parameters list containing arguments. </param>
        private void InternalFireQueued(TTrigger trigger, params object[] args)
        {
            // Add trigger to queue
            _eventQueue.Enqueue(new QueuedTrigger { Trigger = trigger, Args = args });

            // If a trigger is already being handled then the trigger will be queued (FIFO) and processed later.
            if (_firing)
            {
                return;
            }

            try
            {
                _firing = true;

                // Empty queue for triggers
                while (_eventQueue.Any())
                {
                    var queuedEvent = _eventQueue.Dequeue();
                    InternalFireOne(queuedEvent.Trigger, queuedEvent.Args);
                }
            }
            finally
            {
                _firing = false;
            }
        }

        /// <summary>
        /// This method handles the execution of a trigger handler. It finds a handle, then updates the current state information.
        /// 此方法负责处理触发器处理器的执行。它会先找到一个句柄，然后更新当前的状态信息。
        /// </summary>
        /// <param name="trigger"></param>
        /// <param name="args"></param>
        private void InternalFireOne(TTrigger trigger, params object[] args)
        {
            // If this is a trigger with parameters, we must validate the parameter(s)
            if (_triggerConfiguration.TryGetValue(trigger, out TriggerWithParameters configuration))
            {
                configuration.ValidateParameters(args);
            }

            var source = State;
            var representativeState = GetRepresentation(source);

            // Try to find a trigger handler, either in the current state or a super state.
            if (!representativeState.TryFindHandler(trigger, args, out TriggerBehaviourResult result))
            {
                _unhandledTriggerAction.Execute(representativeState.UnderlyingState, trigger, result?.UnmetGuardConditions);
                return;
            }

            switch (result.Handler)
            {
                // Check if this trigger should be ignored
                case IgnoredTriggerBehaviour _:
                    return;
                // Handle special case, re-entry in superstate
                // Check if it is an internal transition, or a transition from one state to another.
                case ReentryTriggerBehaviour handler:
                    {
                        // Handle transition, and set new state
                        var transition = new Transition(source, handler.Destination, trigger, args);
                        HandleReentryTrigger(args, representativeState, transition);
                        break;
                    }
                case DynamicTriggerBehaviourAsync asyncHandler:
                    {
                        asyncHandler.GetDestinationState(source, args)
                            .ContinueWith(t =>
                            {
                                var destination = t.Result;
                                // Handle transition, and set new state; reentry is permitted from dynamic trigger behaviours.
                                var transition = new Transition(source, destination, trigger, args);
                                return HandleTransitioningTriggerAsync(args, representativeState, transition);
                            });
                        break;
                    }
                case DynamicTriggerBehaviour handler:
                    {
                        handler.GetDestinationState(source, args, out var destination);
                        // Handle transition, and set new state; reentry is permitted from dynamic trigger behaviours.
                        var transition = new Transition(source, destination, trigger, args);
                        HandleTransitioningTrigger(args, representativeState, transition);

                        break;
                    }
                case TransitioningTriggerBehaviour handler:
                    {
                        // If a trigger was found on a superstate that would cause unintended reentry, don't trigger.
                        if (source.Equals(handler.Destination))
                            break;

                        // Handle transition, and set new state
                        var transition = new Transition(source, handler.Destination, trigger, args);
                        HandleTransitioningTrigger(args, representativeState, transition);

                        break;
                    }
                case InternalTriggerBehaviour _:
                    {
                        // Internal transitions does not update the current state, but must execute the associated action.
                        var transition = new Transition(source, source, trigger, args);
                        CurrentRepresentation.InternalAction(transition, args);
                        break;
                    }
                default:
                    throw new InvalidOperationException("State machine configuration incorrect, no handler for trigger.");
            }
        }

        private void HandleReentryTrigger(object[] args, StateRepresentation representativeState, Transition transition)
        {
            StateRepresentation representation;
            transition = representativeState.Exit(transition);
            var newRepresentation = GetRepresentation(transition.Destination);

            if (!transition.Source.Equals(transition.Destination))
            {
                // Then Exit the final superstate
                transition = new Transition(transition.Destination, transition.Destination, transition.Trigger, args);
                newRepresentation.Exit(transition);

                _onTransitionedEvent.Invoke(transition);
                representation = EnterState(newRepresentation, transition, args);
                _onTransitionCompletedEvent.Invoke(transition);

            }
            else
            {
                _onTransitionedEvent.Invoke(transition);
                representation = EnterState(newRepresentation, transition, args);
                _onTransitionCompletedEvent.Invoke(transition);
            }
            State = representation.UnderlyingState;
        }

        private void HandleTransitioningTrigger(object[] args, StateRepresentation representativeState, Transition transition)
        {
            transition = representativeState.Exit(transition);

            State = transition.Destination;
            var newRepresentation = GetRepresentation(transition.Destination);

            //Alert all listeners of state transition
            _onTransitionedEvent.Invoke(transition);
            var representation = EnterState(newRepresentation, transition, args);

            // Check if state has changed by entering new state (by firing triggers in OnEntry or such)
            if (!representation.UnderlyingState.Equals(State))
            {
                // The state has been changed after entering the state, must update current state to new one
                State = representation.UnderlyingState;
            }

            _onTransitionCompletedEvent.Invoke(new Transition(transition.Source, State, transition.Trigger, transition.Parameters));
        }

        private StateRepresentation EnterState(StateRepresentation representation, Transition transition, object[] args)
        {
            // Enter the new state
            representation.Enter(transition, args);

            if (FiringMode.Immediate.Equals(_firingMode) && !State.Equals(transition.Destination))
            {
                // This can happen if triggers are fired in OnEntry
                // Must update current representation with updated State
                representation = GetRepresentation(State);
                transition = new Transition(transition.Source, State, transition.Trigger, args);
            }

            // Recursively enter substates that have an initial transition
            if (representation.HasInitialTransition)
            {
                // Verify that the target state is a substate
                // Check if state has substate(s), and if an initial transition(s) has been set up.
                if (!representation.GetSubstates().Any(s => s.UnderlyingState.Equals(representation.InitialTransitionTarget)))
                {
                    throw new InvalidOperationException($"The target ({representation.InitialTransitionTarget}) for the initial transition is not a substate.");
                }

                var initialTransition = new InitialTransition(transition.Source, representation.InitialTransitionTarget, transition.Trigger, args);
                representation = GetRepresentation(representation.InitialTransitionTarget);

                // Alert all listeners of initial state transition
                _onTransitionedEvent.Invoke(new Transition(transition.Destination, initialTransition.Destination, transition.Trigger, transition.Parameters));
                representation = EnterState(representation, initialTransition, args);
            }

            return representation;
        }

        /// <summary>
        /// Override the default behaviour of throwing an exception when an unhandled trigger is fired.
        /// 当触发未处理的触发器时，重写默认抛出异常的行为。
        /// </summary>
        /// <param name="unhandledTriggerAction">An action to call when an unhandled trigger is fired.</param>
        public void OnUnhandledTrigger(Action<TState, TTrigger> unhandledTriggerAction)
        {
            if (unhandledTriggerAction == null) throw new ArgumentNullException(nameof(unhandledTriggerAction));
            _unhandledTriggerAction = new UnhandledTriggerAction.Sync((s, t, c) => unhandledTriggerAction(s, t));
        }

        /// <summary>
        /// Override the default behaviour of throwing an exception when an unhandled trigger is fired.
        /// 当触发未处理的触发器时，重写默认抛出异常的行为。
        /// </summary>
        /// <param name="unhandledTriggerAction">An action to call when an unhandled trigger is fired.</param>
        public void OnUnhandledTrigger(Action<TState, TTrigger, ICollection<string>> unhandledTriggerAction)
        {
            if (unhandledTriggerAction == null) throw new ArgumentNullException(nameof(unhandledTriggerAction));
            _unhandledTriggerAction = new UnhandledTriggerAction.Sync(unhandledTriggerAction);
        }

        /// <summary>
        /// Determine if the state machine is in the supplied state.
        /// 确定状态机是否处于所提供的状态。
        /// </summary>
        /// <param name="state">The state to test for.</param>
        /// <returns>True if the current state is equal to, or a substate of,
        /// the supplied state.</returns>
        public bool IsInState(TState state)
        {
            return CurrentRepresentation.IsIncludedIn(state);
        }

        /// <summary>
        /// Returns true if <paramref name="trigger"/> can be fired in the current state.
        /// 确定当前状态可以触发所提供的触发器
        /// </summary>
        /// <remarks>
        /// When the trigger is configured with parameters, the <c>default</c> value of each of the trigger parameter's types will be used 
        /// to evaluate whether it can fire, which may not be the desired behavior; to check if a trigger can be fired with specific arguments,
        /// use the overload of <c>CanFire&lt;TArg1[, TArg2[ ,TArg3]]&gt;(TriggerWithParameters&lt;TArg1[, TArg2[ ,TArg3]]&gt;, ...)</c> that
        /// matches the type arguments of your trigger.
        /// 当触发器配置了参数时，每个触发器参数类型的 <c>default</c> 值将用于评估其是否可以触发，这可能并非预期行为；若要检查触发器能否使用特定参数触发，
        /// 请使用与触发器类型参数匹配的 <c>CanFire<TArg1[, TArg2[ ,TArg3]]>(TriggerWithParameters<TArg1[, TArg2[ ,TArg3]]>, ...)</c> 重载方法。
        /// </remarks>
        /// <param name="trigger">Trigger to test.</param>
        /// <returns>True if the trigger can be fired, false otherwise.</returns>
        public bool CanFire(TTrigger trigger)
        {
            return CurrentRepresentation.CanHandle(trigger);
        }

        /// <summary>
        /// Returns true if <paramref name="trigger"/> can be fired
        /// in the current state using the supplied trigger argument.
        /// 确定当前状态可以触发所提供的触发器
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <param name="trigger">Trigger to test.</param>
        /// <param name="arg0">The first argument.</param>
        /// <returns>True if the trigger can be fired, false otherwise.</returns>
        public bool CanFire<TArg0>(TriggerWithParameters<TArg0> trigger, TArg0 arg0)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));

            return CurrentRepresentation.CanHandle(trigger.Trigger, arg0);
        }

        /// <summary>
        /// Returns true if <paramref name="trigger"/> can be fired
        /// in the current state using the supplied trigger arguments.
        /// 确定当前状态可以触发所提供的触发器
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <typeparam name="TArg1">Type of the second trigger argument.</typeparam>
        /// <param name="trigger">Trigger to test.</param>
        /// <param name="arg0">The first argument.</param>
        /// <param name="arg1">The second argument.</param>
        /// <returns>True if the trigger can be fired, false otherwise.</returns>
        public bool CanFire<TArg0, TArg1>(TriggerWithParameters<TArg0, TArg1> trigger, TArg0 arg0, TArg1 arg1)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));

            return CurrentRepresentation.CanHandle(trigger.Trigger, arg0, arg1);
        }

        /// <summary>
        /// Returns true if <paramref name="trigger"/> can be fired
        /// in the current state using the supplied trigger arguments.
        /// 确定当前状态可以触发所提供的触发器
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <typeparam name="TArg1">Type of the second trigger argument.</typeparam>
        /// <typeparam name="TArg2">Type of the third trigger argument.</typeparam>
        /// <param name="trigger">Trigger to test.</param>
        /// <param name="arg0">The first argument.</param>
        /// <param name="arg1">The second argument.</param>
        /// <param name="arg2">The third argument.</param>
        /// <returns>True if the trigger can be fired, false otherwise.</returns>
        public bool CanFire<TArg0, TArg1, TArg2>(TriggerWithParameters<TArg0, TArg1, TArg2> trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));

            return CurrentRepresentation.CanHandle(trigger.Trigger, arg0, arg1, arg2);
        }

        /// <summary>
        /// Returns true if <paramref name="trigger"/> can be fired in the current state.
        /// 确定当前状态可以触发所提供的触发器
        /// </summary>
        /// <remarks>
        /// When the trigger is configured with parameters, the <c>default</c> value of each of the trigger parameter's types will be used 
        /// to evaluate whether it can fire, which may not be the desired behavior; to check if a trigger can be fired with specific arguments,
        /// use the overload of <c>CanFire&lt;TArg1[, TArg2[ ,TArg3]]&gt;(TriggerWithParameters&lt;TArg1[, TArg2[ ,TArg3]]&gt;, ...)</c> that
        /// matches the type arguments of your trigger.
        /// </remarks>
        /// <param name="trigger">Trigger to test.</param>
        /// <param name="unmetGuards">Guard descriptions of unmet guards. If given trigger is not configured for current state, this will be null.</param>
        /// <returns>True if the trigger can be fired, false otherwise.</returns>
        public bool CanFire(TTrigger trigger, out ICollection<string> unmetGuards)
        {
            return CurrentRepresentation.CanHandle(trigger, new object[] { }, out unmetGuards);
        }

        /// <summary>
        /// Returns true if <paramref name="trigger"/> can be fired
        /// in the current state using the supplied trigger argument.
        /// 确定当前状态可以触发所提供的触发器
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <param name="trigger">Trigger to test.</param>
        /// <param name="arg0">The first argument.</param>
        /// <param name="unmetGuards">Guard descriptions of unmet guards. If given trigger is not configured for current state, this will be null.</param>
        /// <returns>True if the trigger can be fired, false otherwise.</returns>
        public bool CanFire<TArg0>(TriggerWithParameters<TArg0> trigger, TArg0 arg0, out ICollection<string> unmetGuards)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));

            return CurrentRepresentation.CanHandle(trigger.Trigger, new object[] { arg0 }, out unmetGuards);
        }

        /// <summary>
        /// Returns true if <paramref name="trigger"/> can be fired
        /// in the current state using the supplied trigger arguments.
        /// 确定当前状态可以触发所提供的触发器
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <typeparam name="TArg1">Type of the second trigger argument.</typeparam>
        /// <param name="trigger">Trigger to test.</param>
        /// <param name="arg0">The first argument.</param>
        /// <param name="arg1">The second argument.</param>
        /// <param name="unmetGuards">Guard descriptions of unmet guards. If given trigger is not configured for current state, this will be null.</param>
        /// <returns>True if the trigger can be fired, false otherwise.</returns>
        public bool CanFire<TArg0, TArg1>(TriggerWithParameters<TArg0, TArg1> trigger, TArg0 arg0, TArg1 arg1, out ICollection<string> unmetGuards)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));

            return CurrentRepresentation.CanHandle(trigger.Trigger, new object[] { arg0, arg1 }, out unmetGuards);
        }

        /// <summary>
        /// Returns true if <paramref name="trigger"/> can be fired
        /// in the current state using the supplied trigger arguments.
        /// 确定当前状态可以触发所提供的触发器
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <typeparam name="TArg1">Type of the second trigger argument.</typeparam>
        /// <typeparam name="TArg2">Type of the third trigger argument.</typeparam>
        /// <param name="trigger">Trigger to test.</param>
        /// <param name="arg0">The first argument.</param>
        /// <param name="arg1">The second argument.</param>
        /// <param name="arg2">The third argument.</param>
        /// <param name="unmetGuards">Guard descriptions of unmet guards. If given trigger is not configured for current state, this will be null.</param>
        /// <returns>True if the trigger can be fired, false otherwise.</returns>
        public bool CanFire<TArg0, TArg1, TArg2>(TriggerWithParameters<TArg0, TArg1, TArg2> trigger, TArg0 arg0, TArg1 arg1, TArg2 arg2, out ICollection<string> unmetGuards)
        {
            if (trigger == null) throw new ArgumentNullException(nameof(trigger));

            return CurrentRepresentation.CanHandle(trigger.Trigger, new object[] { arg0, arg1, arg2 }, out unmetGuards);
        }

        /// <summary>
        /// A human-readable representation of the state machine.
        /// 状态机的人类可读表示形式。
        /// </summary>
        /// <returns>A description of the current state and permitted triggers.</returns>
        public override string ToString()
        {
            return string.Format(
                "StateMachine {{ State = {0}, PermittedTriggers = {{ {1} }}}}",
                State,
                string.Join(", ", GetPermittedTriggers().Select(t => t.ToString()).ToArray()));
        }

        /// <summary>
        /// Specify the arguments that must be supplied when a specific trigger is fired.
        /// 指定触发特定触发器时必须提供的参数。
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <param name="trigger">The underlying trigger value.</param>
        /// <returns>An object that can be passed to the Fire() method in order to
        /// fire the parameterised trigger.</returns>
        public TriggerWithParameters<TArg0> SetTriggerParameters<TArg0>(TTrigger trigger)
        {
            var configuration = new TriggerWithParameters<TArg0>(trigger);
            SaveTriggerConfiguration(configuration);
            return configuration;
        }

        /// <summary>
        /// Specify the arguments that must be supplied when a specific trigger is fired.
        /// 指定触发特定触发器时必须提供的参数。
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <typeparam name="TArg1">Type of the second trigger argument.</typeparam>
        /// <param name="trigger">The underlying trigger value.</param>
        /// <returns>An object that can be passed to the Fire() method in order to
        /// fire the parameterised trigger.</returns>
        public TriggerWithParameters<TArg0, TArg1> SetTriggerParameters<TArg0, TArg1>(TTrigger trigger)
        {
            var configuration = new TriggerWithParameters<TArg0, TArg1>(trigger);
            SaveTriggerConfiguration(configuration);
            return configuration;
        }

        /// <summary>
        /// Specify the arguments that must be supplied when a specific trigger is fired.
        /// 指定触发特定触发器时必须提供的参数。
        /// </summary>
        /// <typeparam name="TArg0">Type of the first trigger argument.</typeparam>
        /// <typeparam name="TArg1">Type of the second trigger argument.</typeparam>
        /// <typeparam name="TArg2">Type of the third trigger argument.</typeparam>
        /// <param name="trigger">The underlying trigger value.</param>
        /// <returns>An object that can be passed to the Fire() method in order to
        /// fire the parameterised trigger.</returns>
        public TriggerWithParameters<TArg0, TArg1, TArg2> SetTriggerParameters<TArg0, TArg1, TArg2>(TTrigger trigger)
        {
            var configuration = new TriggerWithParameters<TArg0, TArg1, TArg2>(trigger);
            SaveTriggerConfiguration(configuration);
            return configuration;
        }

        void SaveTriggerConfiguration(TriggerWithParameters trigger)
        {
            if (_triggerConfiguration.ContainsKey(trigger.Trigger))
                throw new InvalidOperationException(
                    string.Format(StateMachineResources.CannotReconfigureParameters, trigger));

            _triggerConfiguration.Add(trigger.Trigger, trigger);
        }

        void DefaultUnhandledTriggerAction(TState state, TTrigger trigger, ICollection<string> unmetGuardConditions)
        {
            if (unmetGuardConditions?.Any() ?? false)
                throw new InvalidOperationException(
                    string.Format(
                        StateMachineResources.NoTransitionsUnmetGuardConditions,
                        trigger, state, string.Join(", ", unmetGuardConditions)));

            throw new InvalidOperationException(
                string.Format(
                    StateMachineResources.NoTransitionsPermitted,
                    trigger, state));
        }

        /// <summary>
        /// Registers a callback that will be invoked every time the state machine
        /// transitions from one state into another.
        /// 注册一个回调，每当状态机从一个状态转换到另一个状态时，该回调将被调用。
        /// </summary>
        /// <param name="onTransitionAction">The action to execute, accepting the details
        /// of the transition.</param>
        public void OnTransitioned(Action<Transition> onTransitionAction)
        {
            if (onTransitionAction == null) throw new ArgumentNullException(nameof(onTransitionAction));
            _onTransitionedEvent.Register(onTransitionAction);
        }

        /// <summary>
        /// Registers a callback that will be invoked every time the statemachine
        /// transitions from one state into another and all the OnEntryFrom etc methods
        /// have been invoked
        /// 注册一个回调，每当状态机完成从一个状态转换到另一个状态时，该回调将被调用。
        /// </summary>
        /// <param name="onTransitionAction">The action to execute, accepting the details
        /// of the transition.</param>
        public void OnTransitionCompleted(Action<Transition> onTransitionAction)
        {
            if (onTransitionAction == null) throw new ArgumentNullException(nameof(onTransitionAction));
            _onTransitionCompletedEvent.Register(onTransitionAction);
        }
    }
}
