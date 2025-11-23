using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Database;
using UnityEngine;

namespace QuestSystem
{
    /// <summary>
    /// Handles all Firebase persistence operations for quest data.
    /// Provides merge strategy for loading and retry logic for saving.
    /// </summary>
    public class QuestPersistence : MonoBehaviour
    {
        // Retry configuration
        private const int MAX_RETRY_ATTEMPTS = 3;
        private const float RETRY_DELAY_BASE = 1f; // Exponential backoff: 1s, 3s, 9s

        // Error tracking
        private int consecutiveFailures = 0;
        private float lastSuccessfulSaveTime = 0f;

        #region Public API

        /// <summary>
        /// Loads quests from Firebase and merges with existing local data.
        /// Does NOT replace the entire dictionary - preserves unsaved local changes.
        /// </summary>
        /// <param name="nickname">Player nickname</param>
        /// <param name="existingQuests">Existing local quest dictionary (will be merged into)</param>
        /// <param name="dirtyQuestIds">Quest IDs that have unsaved local changes (won't be overwritten)</param>
        /// <returns>True if load succeeded, false otherwise</returns>
        public async Task<bool> LoadQuestsAsync(
            string nickname,
            Dictionary<string, PlayerQuest> existingQuests,
            HashSet<string> dirtyQuestIds = null)
        {
            if (string.IsNullOrEmpty(nickname))
            {
                LogError("LoadQuestsAsync: Nickname is null or empty");
                return false;
            }

            if (existingQuests == null)
            {
                LogError("LoadQuestsAsync: existingQuests dictionary is null");
                return false;
            }

            try
            {
                var database = FirebaseManager.Instance?.GetDatabase();
                if (database == null)
                {
                    LogError("LoadQuestsAsync: Firebase database not available");
                    return false;
                }

                DatabaseReference questRef = database.GetReference("players").Child(nickname).Child("quests");
                var snapshot = await questRef.GetValueAsync();

                if (!snapshot.Exists || snapshot.ChildrenCount == 0)
                {
                    Log($"LoadQuestsAsync: No quest data found for {nickname}");
                    return true; // Not an error - new player
                }

                int mergedCount = 0;
                int skippedCount = 0;

                foreach (var questSnapshot in snapshot.Children)
                {
                    try
                    {
                        string questId = questSnapshot.Key;
                        if (string.IsNullOrEmpty(questId)) continue;

                        // Skip if this quest has unsaved local changes (dirty)
                        if (dirtyQuestIds != null && dirtyQuestIds.Contains(questId))
                        {
                            skippedCount++;
                            Log($"LoadQuestsAsync: Skipping '{questId}' (has unsaved local changes)");
                            continue;
                        }

                        var questDict = questSnapshot.Value as Dictionary<string, object>;
                        PlayerQuest loadedQuest = ParsePlayerQuestFromDict(questDict);

                        if (loadedQuest != null)
                        {
                            loadedQuest.questId = questId;

                            // MERGE STRATEGY: Only overwrite if Firebase data is newer or doesn't exist locally
                            if (!existingQuests.ContainsKey(questId))
                            {
                                existingQuests[questId] = loadedQuest;
                                mergedCount++;
                            }
                            else
                            {
                                // Compare and take newer version
                                // For now, Firebase is always considered authoritative unless dirty
                                existingQuests[questId] = loadedQuest;
                                mergedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"LoadQuestsAsync: Failed to parse quest {questSnapshot.Key}: {ex.Message}");
                        // Continue with other quests
                    }
                }

                Log($"LoadQuestsAsync: Successfully loaded {mergedCount} quests, skipped {skippedCount} dirty quests");
                return true;
            }
            catch (Exception ex)
            {
                LogError($"LoadQuestsAsync: Failed to load quests for {nickname}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves specific quests to Firebase with retry logic.
        /// Uses exponential backoff: 1s, 3s, 9s delays between retries.
        /// </summary>
        /// <param name="nickname">Player nickname</param>
        /// <param name="questsToSave">Dictionary of quests to save</param>
        /// <param name="questIds">Specific quest IDs to save (null = save all)</param>
        /// <returns>True if save succeeded, false otherwise</returns>
        public async Task<bool> SaveQuestsAsync(
            string nickname,
            Dictionary<string, PlayerQuest> questsToSave,
            List<string> questIds = null)
        {
            if (string.IsNullOrEmpty(nickname))
            {
                LogError("SaveQuestsAsync: Nickname is null or empty");
                return false;
            }

            if (questsToSave == null || questsToSave.Count == 0)
            {
                Log("SaveQuestsAsync: No quests to save");
                return true; // Not an error
            }

            // Filter quests if specific IDs provided
            var filteredQuests = questIds != null
                ? questsToSave.Where(q => questIds.Contains(q.Key)).ToDictionary(q => q.Key, q => q.Value)
                : questsToSave;

            if (filteredQuests.Count == 0)
            {
                Log("SaveQuestsAsync: No matching quests to save after filtering");
                return true;
            }

            // Retry logic with exponential backoff
            for (int attempt = 0; attempt < MAX_RETRY_ATTEMPTS; attempt++)
            {
                try
                {
                    bool success = await AttemptSaveAsync(nickname, filteredQuests);

                    if (success)
                    {
                        consecutiveFailures = 0;
                        lastSuccessfulSaveTime = Time.time;
                        Log($"SaveQuestsAsync: Successfully saved {filteredQuests.Count} quests (attempt {attempt + 1})");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogError($"SaveQuestsAsync: Attempt {attempt + 1} failed: {ex.Message}");

                    // If not last attempt, wait with exponential backoff
                    if (attempt < MAX_RETRY_ATTEMPTS - 1)
                    {
                        float delay = RETRY_DELAY_BASE * Mathf.Pow(3, attempt); // 1s, 3s, 9s
                        Log($"SaveQuestsAsync: Retrying in {delay}s...");
                        await Task.Delay((int)(delay * 1000));
                    }
                }
            }

            // All retries failed
            consecutiveFailures++;
            LogError($"SaveQuestsAsync: Failed after {MAX_RETRY_ATTEMPTS} attempts. Consecutive failures: {consecutiveFailures}");
            return false;
        }

        /// <summary>
        /// Blocking save for critical scenarios (OnDestroy, OnApplicationQuit).
        /// Uses Task.Wait() to ensure save completes before app closes.
        /// </summary>
        public bool SaveQuestsBlocking(
            string nickname,
            Dictionary<string, PlayerQuest> questsToSave,
            List<string> questIds = null)
        {
            try
            {
                Log("SaveQuestsBlocking: Starting blocking save...");
                Task<bool> saveTask = SaveQuestsAsync(nickname, questsToSave, questIds);

                // Wait with timeout (10 seconds max)
                bool completed = saveTask.Wait(TimeSpan.FromSeconds(10));

                if (!completed)
                {
                    LogError("SaveQuestsBlocking: Save timed out after 10 seconds");
                    return false;
                }

                return saveTask.Result;
            }
            catch (Exception ex)
            {
                LogError($"SaveQuestsBlocking: Exception during blocking save: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Private Methods

        private async Task<bool> AttemptSaveAsync(string nickname, Dictionary<string, PlayerQuest> quests)
        {
            if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
            {
                LogError("AttemptSaveAsync: Firebase not ready");
                return false;
            }

            var database = FirebaseManager.Instance.GetDatabase();
            if (database == null)
            {
                LogError("AttemptSaveAsync: Firebase database is null");
                return false;
            }

            // Serialize quest data
            var questsData = new Dictionary<string, object>();

            foreach (var questPair in quests)
            {
                string questId = questPair.Key;
                PlayerQuest playerQuest = questPair.Value;

                var questData = SerializeQuestData(questId, playerQuest);
                if (questData != null)
                {
                    questsData[questId] = questData;
                }
            }

            // Save to Firebase
            DatabaseReference questRef = database.GetReference("players").Child(nickname).Child("quests");

            // Update only the specified quests (not replace entire tree)
            await questRef.UpdateChildrenAsync(questsData);

            return true;
        }

        private Dictionary<string, object> SerializeQuestData(string questId, PlayerQuest playerQuest)
        {
            try
            {
                var questData = new Dictionary<string, object>
                {
                    { "questId", questId },
                    { "status", (int)playerQuest.status },
                    { "isHiddenObjectiveActive", playerQuest.isHiddenObjectiveActive }
                };

                // Serialize objectives
                var objectivesData = new List<object>();
                if (playerQuest.objectives != null)
                {
                    foreach (var objective in playerQuest.objectives)
                    {
                        if (objective == null) continue;

                        var objectiveData = new Dictionary<string, object>
                        {
                            { "type", (int)objective.type },
                            { "targetId", objective.targetId ?? "" },
                            { "requiredAmount", objective.requiredAmount },
                            { "currentAmount", objective.currentAmount },
                            { "description", objective.description ?? "" },
                            { "useCompass", objective.useCompass },
                            { "compassCoordinates", objective.compassCoordinates ?? "" },
                            { "requiresItemGive", objective.requiresItemGive },
                            { "requiredItemId", objective.requiredItemId ?? "" },
                            { "requiredItemAmount", objective.requiredItemAmount },
                            { "minUpgradeLevel", objective.minUpgradeLevel }
                        };

                        if (objective.alternativeTargetIds != null && objective.alternativeTargetIds.Length > 0)
                            objectiveData["alternativeTargetIds"] = objective.alternativeTargetIds;

                        if (objective.objectiveDialogues != null && objective.objectiveDialogues.Length > 0)
                            objectiveData["objectiveDialogues"] = objective.objectiveDialogues;

                        objectivesData.Add(objectiveData);
                    }
                }
                questData["objectives"] = objectivesData;

                // Serialize hidden objective
                if (playerQuest.hiddenObjective != null)
                {
                    var hiddenObjData = new Dictionary<string, object>
                    {
                        { "type", (int)playerQuest.hiddenObjective.type },
                        { "targetId", playerQuest.hiddenObjective.targetId ?? "" },
                        { "requiredAmount", playerQuest.hiddenObjective.requiredAmount },
                        { "currentAmount", playerQuest.hiddenObjective.currentAmount },
                        { "description", playerQuest.hiddenObjective.description ?? "" },
                        { "useCompass", playerQuest.hiddenObjective.useCompass },
                        { "compassCoordinates", playerQuest.hiddenObjective.compassCoordinates ?? "" },
                        { "requiresItemGive", playerQuest.hiddenObjective.requiresItemGive },
                        { "requiredItemId", playerQuest.hiddenObjective.requiredItemId ?? "" },
                        { "requiredItemAmount", playerQuest.hiddenObjective.requiredItemAmount },
                        { "minUpgradeLevel", playerQuest.hiddenObjective.minUpgradeLevel }
                    };

                    if (playerQuest.hiddenObjective.alternativeTargetIds != null &&
                        playerQuest.hiddenObjective.alternativeTargetIds.Length > 0)
                        hiddenObjData["alternativeTargetIds"] = playerQuest.hiddenObjective.alternativeTargetIds;

                    if (playerQuest.hiddenObjective.objectiveDialogues != null &&
                        playerQuest.hiddenObjective.objectiveDialogues.Length > 0)
                        hiddenObjData["objectiveDialogues"] = playerQuest.hiddenObjective.objectiveDialogues;

                    questData["hiddenObjective"] = hiddenObjData;
                }

                return questData;
            }
            catch (Exception ex)
            {
                LogError($"SerializeQuestData: Failed to serialize quest {questId}: {ex.Message}");
                return null;
            }
        }

        private PlayerQuest ParsePlayerQuestFromDict(Dictionary<string, object> questDict)
        {
            if (questDict == null) return null;

            try
            {
                PlayerQuest playerQuest = new PlayerQuest();

                // Parse status
                if (questDict.TryGetValue("status", out object statusObj))
                    playerQuest.status = (QuestStatus)Convert.ToInt32(statusObj);

                // Parse hidden objective active flag
                if (questDict.TryGetValue("isHiddenObjectiveActive", out object hiddenActiveObj))
                    playerQuest.isHiddenObjectiveActive = Convert.ToBoolean(hiddenActiveObj);

                // Parse objectives
                if (questDict.TryGetValue("objectives", out object objectivesObj))
                {
                    if (objectivesObj is List<object> objectivesList)
                    {
                        foreach (var objItem in objectivesList)
                        {
                            if (objItem is Dictionary<string, object> objectiveDict)
                            {
                                QuestObjective objective = ParseObjectiveFromDict(objectiveDict);
                                if (objective != null)
                                {
                                    playerQuest.objectives.Add(objective);
                                }
                            }
                        }
                    }
                }

                // Parse hidden objective
                if (questDict.TryGetValue("hiddenObjective", out object hiddenObjData) &&
                    hiddenObjData is Dictionary<string, object> hiddenDict)
                {
                    playerQuest.hiddenObjective = ParseObjectiveFromDict(hiddenDict);
                }

                return playerQuest;
            }
            catch (Exception ex)
            {
                LogError($"ParsePlayerQuestFromDict: Failed to parse quest: {ex.Message}");
                return null;
            }
        }

        private QuestObjective ParseObjectiveFromDict(Dictionary<string, object> objectiveDict)
        {
            if (objectiveDict == null) return null;

            try
            {
                QuestObjective objective = new QuestObjective();

                if (objectiveDict.TryGetValue("type", out object typeObj))
                    objective.type = (QuestType)Convert.ToInt32(typeObj);

                if (objectiveDict.TryGetValue("targetId", out object targetIdObj))
                    objective.targetId = targetIdObj?.ToString() ?? "";

                if (objectiveDict.TryGetValue("alternativeTargetIds", out object altTargetsObj) &&
                    altTargetsObj is List<object> altTargetsList)
                    objective.alternativeTargetIds = altTargetsList.Select(t => t?.ToString() ?? "").ToArray();

                if (objectiveDict.TryGetValue("requiredAmount", out object requiredAmountObj))
                    objective.requiredAmount = Convert.ToInt32(requiredAmountObj);

                if (objectiveDict.TryGetValue("currentAmount", out object currentAmountObj))
                    objective.currentAmount = Convert.ToInt32(currentAmountObj);

                if (objectiveDict.TryGetValue("description", out object descriptionObj))
                    objective.description = descriptionObj?.ToString() ?? "";

                if (objectiveDict.TryGetValue("useCompass", out object useCompassObj))
                    objective.useCompass = Convert.ToBoolean(useCompassObj);

                if (objectiveDict.TryGetValue("compassCoordinates", out object compassCoordsObj))
                    objective.compassCoordinates = compassCoordsObj?.ToString() ?? "";

                if (objectiveDict.TryGetValue("requiresItemGive", out object requiresItemGiveObj))
                    objective.requiresItemGive = Convert.ToBoolean(requiresItemGiveObj);

                if (objectiveDict.TryGetValue("requiredItemId", out object requiredItemIdObj))
                    objective.requiredItemId = requiredItemIdObj?.ToString() ?? "";

                if (objectiveDict.TryGetValue("requiredItemAmount", out object requiredItemAmountObj))
                    objective.requiredItemAmount = Convert.ToInt32(requiredItemAmountObj);
                else
                    objective.requiredItemAmount = 1;

                if (objectiveDict.TryGetValue("objectiveDialogues", out object objDialoguesObj) &&
                    objDialoguesObj is List<object> dialoguesList)
                    objective.objectiveDialogues = dialoguesList.Select(d => d?.ToString() ?? "").ToArray();

                if (objectiveDict.TryGetValue("minUpgradeLevel", out object minUpgradeLevelObj))
                    objective.minUpgradeLevel = Convert.ToInt32(minUpgradeLevelObj);
                else
                    objective.minUpgradeLevel = 0;

                return objective;
            }
            catch (Exception ex)
            {
                LogError($"ParseObjectiveFromDict: Failed to parse objective: {ex.Message}");
                return null;
            }
        }

        #endregion

        #region Logging (Background Only)

        private void Log(string message)
        {
        }

        private void LogError(string message)
        {
        }

        #endregion
    }
}
