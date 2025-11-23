using System;
using System.Collections;
using UnityEngine;

namespace QuestSystem
{
    /// <summary>
    /// Manages quest system initialization state and prevents operations
    /// during unstable initialization periods.
    /// </summary>
    public class QuestStateManager : MonoBehaviour
    {
        // State machine
        public enum State
        {
            Uninitialized,  // Quest system not started
            Loading,        // Loading quests from Firebase
            Ready,          // Fully initialized and ready for operations
            Error           // Initialization failed
        }

        // Current state
        private State currentState = State.Uninitialized;

        // Events
        public event Action OnReady;
        public event Action OnError;

        // Timeout configuration
        private const float INITIALIZATION_TIMEOUT = 10f; // 10 seconds max
        private Coroutine timeoutCoroutine;

        #region Public API

        /// <summary>
        /// Current initialization state
        /// </summary>
        public State CurrentState
        {
            get => currentState;
            private set
            {
                if (currentState != value)
                {
                    State oldState = currentState;
                    currentState = value;
                    OnStateChanged(oldState, value);
                }
            }
        }

        /// <summary>
        /// True if quest system is ready for operations
        /// </summary>
        public bool IsReady => CurrentState == State.Ready;

        /// <summary>
        /// True if quest system is currently loading
        /// </summary>
        public bool IsLoading => CurrentState == State.Loading;

        /// <summary>
        /// True if quest system encountered an error
        /// </summary>
        public bool HasError => CurrentState == State.Error;

        /// <summary>
        /// Starts the loading state and timeout timer
        /// </summary>
        public void BeginLoading()
        {
            if (CurrentState != State.Uninitialized)
            {
                LogWarning($"BeginLoading called in state {CurrentState}, resetting to Loading");
            }

            CurrentState = State.Loading;

            // Start timeout coroutine
            if (timeoutCoroutine != null)
            {
                StopCoroutine(timeoutCoroutine);
            }
            timeoutCoroutine = StartCoroutine(LoadingTimeoutCoroutine());
        }

        /// <summary>
        /// Marks initialization as complete and ready
        /// </summary>
        public void SetReady()
        {
            if (CurrentState == State.Ready)
            {
                LogWarning("SetReady called but already ready");
                return;
            }

            // Stop timeout
            if (timeoutCoroutine != null)
            {
                StopCoroutine(timeoutCoroutine);
                timeoutCoroutine = null;
            }

            CurrentState = State.Ready;

            // Fire ready event
            Log("Quest system ready");
            OnReady?.Invoke();
        }

        /// <summary>
        /// Marks initialization as failed
        /// </summary>
        public void SetError(string errorMessage = null)
        {
            if (CurrentState == State.Error)
            {
                LogWarning("SetError called but already in error state");
                return;
            }

            // Stop timeout
            if (timeoutCoroutine != null)
            {
                StopCoroutine(timeoutCoroutine);
                timeoutCoroutine = null;
            }

            CurrentState = State.Error;

            // Fire error event
            LogError($"Quest system error: {errorMessage ?? "Unknown error"}");
            OnError?.Invoke();
        }

        /// <summary>
        /// Resets state machine to uninitialized (for reconnection scenarios)
        /// </summary>
        public void Reset()
        {
            if (timeoutCoroutine != null)
            {
                StopCoroutine(timeoutCoroutine);
                timeoutCoroutine = null;
            }

            CurrentState = State.Uninitialized;
            Log("Quest state manager reset");
        }

        /// <summary>
        /// Waits until quest system is ready (for guarding operations)
        /// </summary>
        public IEnumerator WaitUntilReady(float timeout = 15f)
        {
            float startTime = Time.time;

            while (!IsReady && Time.time - startTime < timeout)
            {
                if (HasError)
                {
                    LogError("WaitUntilReady: Quest system in error state");
                    yield break;
                }

                yield return new WaitForSeconds(0.1f);
            }

            if (!IsReady)
            {
                LogError($"WaitUntilReady: Timeout after {timeout} seconds");
            }
        }

        /// <summary>
        /// Executes action when quest system is ready.
        /// If already ready, executes immediately.
        /// If loading, queues for execution on ready.
        /// </summary>
        public void ExecuteWhenReady(Action action)
        {
            if (action == null) return;

            if (IsReady)
            {
                // Already ready, execute immediately
                action.Invoke();
            }
            else if (HasError)
            {
                LogError("ExecuteWhenReady: Cannot execute, quest system in error state");
            }
            else
            {
                // Queue for execution when ready
                OnReady += () => action.Invoke();
                Log("Action queued for execution when ready");
            }
        }

        #endregion

        #region Private Methods

        private void OnStateChanged(State oldState, State newState)
        {
            Log($"State transition: {oldState} → {newState}");

            // Clean up one-time event listeners on terminal states
            if (newState == State.Ready || newState == State.Error)
            {
                // Events will persist for ExecuteWhenReady pattern
            }
        }

        private IEnumerator LoadingTimeoutCoroutine()
        {
            yield return new WaitForSeconds(INITIALIZATION_TIMEOUT);

            if (CurrentState == State.Loading)
            {
                LogError($"Initialization timeout after {INITIALIZATION_TIMEOUT} seconds");
                SetError($"Initialization timeout ({INITIALIZATION_TIMEOUT}s)");
            }
        }

        #endregion

        #region Unity Lifecycle

        private void OnDestroy()
        {
            if (timeoutCoroutine != null)
            {
                StopCoroutine(timeoutCoroutine);
            }

            // Clear event listeners
            OnReady = null;
            OnError = null;
        }

        #endregion

        #region Logging

        private void Log(string message)
        {
        }

        private void LogWarning(string message)
        {
        }

        private void LogError(string message)
        {
        }

        #endregion
    }
}
