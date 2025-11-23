using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace QuestSystem
{
    /// <summary>
    /// Manages throttled saving of quest progress to Firebase.
    /// Uses dirty flags and batching to reduce Firebase writes.
    /// Provides guaranteed save on disconnect/quit.
    /// </summary>
    public class QuestSaveQueue : MonoBehaviour
    {
        // Configuration
        private const float BATCH_SAVE_INTERVAL = 3f; // Batch save every 3 seconds
        private const float CRITICAL_SAVE_DELAY = 0.1f; // Slight delay for critical saves to batch rapid events

        // Dependencies (injected)
        private QuestPersistence persistence;
        private Dictionary<string, PlayerQuest> questsReference;
        private string playerNickname;

        // Dirty tracking
        private HashSet<string> dirtyQuests = new HashSet<string>();
        private HashSet<string> criticalDirtyQuests = new HashSet<string>();

        // Timing
        private float lastBatchSaveTime = 0f;
        private float lastCriticalSaveTime = 0f;
        private bool hasPendingSave = false;
        private bool isSaving = false;

        // Statistics
        private int totalSaves = 0;
        private int totalQuestsSaved = 0;
        private int batchSaves = 0;
        private int criticalSaves = 0;

        #region Initialization

        /// <summary>
        /// Initializes the save queue with required dependencies
        /// </summary>
        public void Initialize(
            QuestPersistence persistenceComponent,
            Dictionary<string, PlayerQuest> quests,
            string nickname)
        {
            if (persistenceComponent == null)
            {
                LogError("Initialize: persistence component is null");
                return;
            }

            if (quests == null)
            {
                LogError("Initialize: quests dictionary is null");
                return;
            }

            if (string.IsNullOrEmpty(nickname))
            {
                LogError("Initialize: nickname is null or empty");
                return;
            }

            persistence = persistenceComponent;
            questsReference = quests;
            playerNickname = nickname;

            Log($"Initialized for player '{nickname}'");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Queues a quest for saving.
        /// Critical saves are prioritized and saved more quickly.
        /// Non-critical saves are batched every 3 seconds.
        /// </summary>
        /// <param name="questId">Quest ID to save</param>
        /// <param name="isCritical">True for immediate save (quest complete, hidden objective, etc.)</param>
        public void QueueSave(string questId, bool isCritical = false)
        {
            if (string.IsNullOrEmpty(questId))
            {
                LogWarning("QueueSave: questId is null or empty");
                return;
            }

            if (persistence == null)
            {
                LogError("QueueSave: Not initialized (persistence is null)");
                return;
            }

            // Mark as dirty
            dirtyQuests.Add(questId);

            if (isCritical)
            {
                criticalDirtyQuests.Add(questId);
                hasPendingSave = true;

                Log($"Queued CRITICAL save for quest '{questId}'");

                // Critical saves happen almost immediately but with slight delay to batch rapid events
                // e.g., if 3 objectives complete simultaneously, batch them
                if (Time.time - lastCriticalSaveTime >= CRITICAL_SAVE_DELAY && !isSaving)
                {
                    SaveCriticalQuestsAsync();
                }
            }
            else
            {
                hasPendingSave = true;
                Log($"Queued save for quest '{questId}' (will batch)");
            }
        }

        /// <summary>
        /// Checks if a quest has unsaved changes (dirty)
        /// </summary>
        public bool IsDirty(string questId)
        {
            return dirtyQuests.Contains(questId);
        }

        /// <summary>
        /// Forces immediate save of all dirty quests (blocking).
        /// Used for OnDestroy, OnApplicationQuit, OnApplicationPause.
        /// </summary>
        public bool ForceSaveBlocking()
        {
            if (dirtyQuests.Count == 0)
            {
                Log("ForceSaveBlocking: No dirty quests to save");
                return true;
            }

            if (persistence == null || questsReference == null || string.IsNullOrEmpty(playerNickname))
            {
                LogError("ForceSaveBlocking: Not initialized properly");
                return false;
            }

            Log($"ForceSaveBlocking: Saving {dirtyQuests.Count} dirty quests...");

            var questsToSave = new List<string>(dirtyQuests);

            bool success = persistence.SaveQuestsBlocking(playerNickname, questsReference, questsToSave);

            if (success)
            {
                // Clear dirty flags
                dirtyQuests.Clear();
                criticalDirtyQuests.Clear();
                hasPendingSave = false;

                Log($"ForceSaveBlocking: Successfully saved {questsToSave.Count} quests");
            }
            else
            {
                LogError("ForceSaveBlocking: Save failed");
            }

            return success;
        }

        /// <summary>
        /// Forces immediate save of all dirty quests (async, non-blocking).
        /// Returns task that completes when save is done.
        /// </summary>
        public async Task<bool> ForceSaveAsync()
        {
            if (dirtyQuests.Count == 0)
            {
                Log("ForceSaveAsync: No dirty quests to save");
                return true;
            }

            if (isSaving)
            {
                LogWarning("ForceSaveAsync: Already saving, skipping");
                return false;
            }

            return await SaveDirtyQuestsAsync(dirtyQuests);
        }

        /// <summary>
        /// Gets statistics about save queue performance
        /// </summary>
        public string GetStatistics()
        {
            return $"Total saves: {totalSaves}, Quests saved: {totalQuestsSaved}, " +
                   $"Batch saves: {batchSaves}, Critical saves: {criticalSaves}, " +
                   $"Dirty quests: {dirtyQuests.Count}";
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (!hasPendingSave || isSaving) return;

            // Check if it's time for batch save
            if (Time.time - lastBatchSaveTime >= BATCH_SAVE_INTERVAL)
            {
                if (dirtyQuests.Count > 0)
                {
                    SaveBatchQuestsAsync();
                }
            }
        }

        private void OnDestroy()
        {
            // Force save on destroy
            if (dirtyQuests.Count > 0)
            {
                LogWarning("OnDestroy: Forcing save of dirty quests");
                ForceSaveBlocking();
            }
        }

        private void OnApplicationQuit()
        {
            // Force save on quit
            if (dirtyQuests.Count > 0)
            {
                LogWarning("OnApplicationQuit: Forcing save of dirty quests");
                ForceSaveBlocking();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            // On Android, save when app goes to background
            if (pauseStatus && dirtyQuests.Count > 0)
            {
                LogWarning("OnApplicationPause: Forcing save of dirty quests");
                ForceSaveBlocking();
            }
        }

        #endregion

        #region Private Save Methods

        /// <summary>
        /// Saves critical quests immediately (with slight batching)
        /// </summary>
        private async void SaveCriticalQuestsAsync()
        {
            if (isSaving)
            {
                Log("SaveCriticalQuestsAsync: Already saving, deferring");
                return;
            }

            if (criticalDirtyQuests.Count == 0)
            {
                return;
            }

            var questsToSave = new HashSet<string>(criticalDirtyQuests);
            criticalDirtyQuests.Clear();

            bool success = await SaveDirtyQuestsAsync(questsToSave);

            if (success)
            {
                criticalSaves++;
                lastCriticalSaveTime = Time.time;
                Log($"SaveCriticalQuestsAsync: Saved {questsToSave.Count} critical quests");
            }
        }

        /// <summary>
        /// Saves batch quests (every 3 seconds)
        /// </summary>
        private async void SaveBatchQuestsAsync()
        {
            if (isSaving)
            {
                Log("SaveBatchQuestsAsync: Already saving, deferring");
                return;
            }

            if (dirtyQuests.Count == 0)
            {
                return;
            }

            var questsToSave = new HashSet<string>(dirtyQuests);

            bool success = await SaveDirtyQuestsAsync(questsToSave);

            if (success)
            {
                batchSaves++;
                lastBatchSaveTime = Time.time;
                Log($"SaveBatchQuestsAsync: Saved {questsToSave.Count} quests in batch");
            }
            else
            {
                // On failure, keep dirty flags so we retry next batch
                LogError("SaveBatchQuestsAsync: Save failed, will retry next batch");
            }
        }

        /// <summary>
        /// Core save logic - saves specified quests and clears dirty flags
        /// </summary>
        private async Task<bool> SaveDirtyQuestsAsync(HashSet<string> questsToSave)
        {
            if (questsToSave == null || questsToSave.Count == 0)
            {
                return true;
            }

            if (persistence == null || questsReference == null || string.IsNullOrEmpty(playerNickname))
            {
                LogError("SaveDirtyQuestsAsync: Not initialized properly");
                return false;
            }

            isSaving = true;

            try
            {
                var questList = new List<string>(questsToSave);

                Log($"SaveDirtyQuestsAsync: Saving {questList.Count} quests...");

                bool success = await persistence.SaveQuestsAsync(playerNickname, questsReference, questList);

                if (success)
                {
                    // Clear dirty flags for successfully saved quests
                    foreach (string questId in questsToSave)
                    {
                        dirtyQuests.Remove(questId);
                        criticalDirtyQuests.Remove(questId);
                    }

                    // Update statistics
                    totalSaves++;
                    totalQuestsSaved += questList.Count;

                    // Clear pending flag if no more dirty quests
                    if (dirtyQuests.Count == 0)
                    {
                        hasPendingSave = false;
                    }

                    Log($"SaveDirtyQuestsAsync: Successfully saved {questList.Count} quests");
                    return true;
                }
                else
                {
                    LogError($"SaveDirtyQuestsAsync: Failed to save {questList.Count} quests");
                    // Keep dirty flags so we retry later
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogError($"SaveDirtyQuestsAsync: Exception during save: {ex.Message}");
                return false;
            }
            finally
            {
                isSaving = false;
            }
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
