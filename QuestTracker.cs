using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

public class QuestTracker : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject questTrackerPanel;
    [SerializeField] private Transform questContainer;
    [SerializeField] private GameObject questEntryPrefab;
    [SerializeField] private GameObject objectivePrefab;
    [SerializeField] private Button toggleButton;
    [SerializeField] private int maxDisplayedQuests = 5;

    private Dictionary<string, GameObject> activeQuestEntries = new Dictionary<string, GameObject>();
    private bool isTrackerVisible = true;

    private void Awake()
    {
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(ToggleTrackerVisibility);
        }
    }

    private void Start()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestStarted += OnQuestStarted;
            QuestManager.Instance.OnQuestUpdated += OnQuestUpdated;
            QuestManager.Instance.OnQuestCompleted += OnQuestCompleted;
            QuestManager.Instance.OnQuestTurnedIn += OnQuestTurnedIn;
            RefreshQuestTracker();
        }

        if (questTrackerPanel != null)
        {
            questTrackerPanel.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        if (QuestManager.Instance != null)
        {
            QuestManager.Instance.OnQuestStarted -= OnQuestStarted;
            QuestManager.Instance.OnQuestUpdated -= OnQuestUpdated;
            QuestManager.Instance.OnQuestCompleted -= OnQuestCompleted;
            QuestManager.Instance.OnQuestTurnedIn -= OnQuestTurnedIn;
        }
    }

    public void RefreshQuestTracker()
    {
        ClearQuestEntries();

        if (QuestManager.Instance != null)
        {
            List<PlayerQuest> activeQuests = QuestManager.Instance.GetActiveQuests();

            if (questTrackerPanel != null && !questTrackerPanel.activeSelf)
            {
                questTrackerPanel.SetActive(true);
            }

            if (activeQuests != null && activeQuests.Count > 0)
            {
                foreach (var quest in activeQuests.Take(maxDisplayedQuests))
                {
                    if (quest == null || string.IsNullOrEmpty(quest.questId))
                    {
                        continue;
                    }

                    QuestData questData = QuestManager.Instance.GetQuestData(quest.questId);
                    if (questData != null)
                    {
                        AddQuestEntry(quest, questData);
                    }
                }
            }
        }
    }

    private void ClearQuestEntries()
    {
        foreach (var entry in activeQuestEntries.Values)
        {
            Destroy(entry);
        }
        activeQuestEntries.Clear();
    }

    private void OnQuestStarted(string questId)
    {
        PlayerQuest quest = QuestManager.Instance.GetPlayerQuest(questId);
        QuestData questData = QuestManager.Instance.GetQuestData(questId);

        if (quest != null && questData != null)
        {
            AddQuestEntry(quest, questData);
        }
    }

    private void OnQuestUpdated(string questId)
    {
        UpdateQuestEntry(questId);
    }

    private void OnQuestCompleted(string questId)
    {
        UpdateQuestEntry(questId);
    }

    private void OnQuestTurnedIn(string questId)
    {
        RemoveQuestEntry(questId);
    }

    private void AddQuestEntry(PlayerQuest quest, QuestData questData)
    {
        if (activeQuestEntries.ContainsKey(quest.questId))
        {
            UpdateQuestEntry(quest.questId);
            return;
        }

        if (activeQuestEntries.Count >= maxDisplayedQuests)
        {
            return;
        }

        GameObject entryObj = Instantiate(questEntryPrefab, questContainer);
        activeQuestEntries[quest.questId] = entryObj;

        Text questName = entryObj.transform.Find("QuestName")?.GetComponent<Text>();
        if (questName == null)
        {
            questName = entryObj.GetComponentInChildren<Text>();
        }

        if (questName != null)
        {
            questName.text = questData.questName;
        }

        Image checkboxBackground = entryObj.transform.Find("Checkbox")?.GetComponent<Image>();
        Image checkmark = entryObj.transform.Find("Checkbox/Checkmark")?.GetComponent<Image>();

        if (checkboxBackground != null && checkmark != null)
        {
            checkboxBackground.gameObject.SetActive(true);
            checkmark.gameObject.SetActive(quest.status == QuestStatus.Completed);
        }

        Text questDescriptionText = entryObj.transform.Find("QuestDescription")?.GetComponent<Text>();
        if (questDescriptionText != null)
        {
            if (!string.IsNullOrEmpty(questData.questDescription))
            {
                questDescriptionText.text = questData.questDescription;
                questDescriptionText.gameObject.SetActive(true);
            }
            else
            {
                questDescriptionText.gameObject.SetActive(false);
            }
        }

        Transform objectivesContainer = entryObj.transform.Find("ObjectivesContainer");
        if (objectivesContainer != null && objectivePrefab != null)
        {
            if (quest.isHiddenObjectiveActive && quest.hiddenObjective != null)
            {
                CreateObjectiveEntry(objectivesContainer, quest.hiddenObjective);
            }
            else
            {
                foreach (var objective in quest.objectives)
                {
                    CreateObjectiveEntry(objectivesContainer, objective);
                }
            }
        }
    }

    private void UpdateQuestEntry(string questId)
    {
        if (!activeQuestEntries.TryGetValue(questId, out GameObject entryObj))
        {
            return;
        }

        PlayerQuest quest = QuestManager.Instance.GetPlayerQuest(questId);
        QuestData questData = QuestManager.Instance.GetQuestData(questId);

        if (quest == null || questData == null)
        {
            return;
        }

        Text questName = entryObj.transform.Find("QuestName")?.GetComponent<Text>();
        if (questName == null)
        {
            questName = entryObj.GetComponentInChildren<Text>();
        }

        if (questName != null)
        {
            questName.text = questData.questName;
        }

        Image checkboxBackground = entryObj.transform.Find("Checkbox")?.GetComponent<Image>();
        Image checkmark = entryObj.transform.Find("Checkbox/Checkmark")?.GetComponent<Image>();

        if (checkboxBackground != null && checkmark != null)
        {
            checkboxBackground.gameObject.SetActive(true);
            checkmark.gameObject.SetActive(quest.status == QuestStatus.Completed);
        }

        Text questDescriptionText = entryObj.transform.Find("QuestDescription")?.GetComponent<Text>();
        if (questDescriptionText != null)
        {
            if (!string.IsNullOrEmpty(questData.questDescription))
            {
                questDescriptionText.text = questData.questDescription;
                questDescriptionText.gameObject.SetActive(true);
            }
            else
            {
                questDescriptionText.gameObject.SetActive(false);
            }
        }

        Transform objectivesContainer = entryObj.transform.Find("ObjectivesContainer");
        if (objectivesContainer != null)
        {
            foreach (Transform child in objectivesContainer)
            {
                Destroy(child.gameObject);
            }

            if (objectivePrefab != null)
            {
                if (quest.isHiddenObjectiveActive && quest.hiddenObjective != null)
                {
                    CreateObjectiveEntry(objectivesContainer, quest.hiddenObjective);
                }
                else
                {
                    foreach (var objective in quest.objectives)
                    {
                        CreateObjectiveEntry(objectivesContainer, objective);
                    }
                }
            }
        }
    }

    private void CreateObjectiveEntry(Transform container, QuestObjective objective)
    {
        GameObject objectiveObj = Instantiate(objectivePrefab, container);
        string targetName = GetObjectiveTargetName(objective);

        Text objectiveText = objectiveObj.GetComponent<Text>();
        if (objectiveText != null)
        {
            bool hasAlternatives = objective.type == QuestType.TalkToNPC &&
                                  objective.alternativeTargetIds != null &&
                                  objective.alternativeTargetIds.Length > 0;

            // KillMonsters ve CollectItems her zaman sayı gösterir (compass olsa bile)
            // ReachLocation ve TalkToNPC sadece isim gösterir
            if (objective.type == QuestType.ReachLocation || hasAlternatives)
            {
                objectiveText.text = objective.currentAmount >= objective.requiredAmount ?
                    $"{targetName} ✓" :
                    $"{targetName}";
            }
            else
            {
                objectiveText.text = $" {targetName}: {objective.currentAmount}/{objective.requiredAmount}";
            }

            objectiveText.color = objective.IsCompleted ? Color.green : Color.white;
        }

        Image objectiveCheckbox = objectiveObj.transform.Find("Checkbox")?.GetComponent<Image>();
        Image objectiveCheckmark = objectiveObj.transform.Find("Checkbox/Checkmark (Complete)")?.GetComponent<Image>();

        if (objectiveCheckbox != null && objectiveCheckmark != null)
        {
            objectiveCheckbox.gameObject.SetActive(true);
            objectiveCheckmark.gameObject.SetActive(false);

            if (objective.currentAmount >= objective.requiredAmount)
            {
                objectiveCheckmark.gameObject.SetActive(true);
            }
        }
    }

    private void RemoveQuestEntry(string questId)
    {
        if (activeQuestEntries.TryGetValue(questId, out GameObject entryObj))
        {
            Destroy(entryObj);
            activeQuestEntries.Remove(questId);
        }
    }

private string GetObjectiveTargetName(QuestObjective objective)
{
    switch (objective.type)
    {
        case QuestType.KillMonsters:
            if (!string.IsNullOrEmpty(objective.description))
            {
                return objective.description;
            }
            return objective.targetId;

        case QuestType.CollectItems:
            // ✅ Önce description kontrolü yap
            if (!string.IsNullOrEmpty(objective.description))
            {
                return objective.description;
            }
            // Description yoksa item ismini göster
            ItemData item = ItemDatabase.Instance.GetItemById(objective.targetId);
            return item != null ? item.itemName : objective.targetId;

        case QuestType.ReachLocation:
            if (!string.IsNullOrEmpty(objective.description))
            {
                if (TryParseLocationForCoordinates(objective.targetId, out Vector2 coords))
                {
                    return $"{objective.description} — [{coords.x:F0}, {coords.y:F0}]";
                }
                else
                {
                    return objective.description;
                }
            }
            else if (TryParseLocationForCoordinates(objective.targetId, out Vector2 coords))
            {
                return $"Konum: [{coords.x:F0}, {coords.y:F0}]";
            }
            return "Belirtilen Konum";

        case QuestType.TalkToNPC:
            if (objective.alternativeTargetIds != null && objective.alternativeTargetIds.Length > 0)
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                if (!string.IsNullOrEmpty(objective.description))
                {
                    sb.AppendLine(objective.description);
                }
                else
                {
                    sb.AppendLine("Aşağıdakilerden biriyle konuş:");
                }

                sb.AppendLine($"  - {objective.targetId} ile konuş");

                foreach (string altNpc in objective.alternativeTargetIds)
                {
                    sb.AppendLine($"  - {altNpc} ile konuş");
                }

                return sb.ToString().TrimEnd();
            }

            if (objective.useCompass && !string.IsNullOrEmpty(objective.compassCoordinates))
            {
                if (TryParseLocationForCoordinates(objective.compassCoordinates, out Vector2 coords))
                {
                    string baseText = !string.IsNullOrEmpty(objective.description) ?
                        objective.description :
                        "NPC: " + objective.targetId;
                    return $"{baseText} — [{coords.x:F0}, {coords.y:F0}]";
                }
            }

            if (!string.IsNullOrEmpty(objective.description))
            {
                return objective.description;
            }
            return "NPC: " + objective.targetId;

        case QuestType.BindToBindstone:
            if (!string.IsNullOrEmpty(objective.description))
            {
                return objective.description;
            }
            return "Bir Bindstone'a kayıt ol";
            
        case QuestType.PickupEquipment:
            if (!string.IsNullOrEmpty(objective.description))
            {
                return objective.description;
            }
            return "Ekipman topla";
            
        case QuestType.BuyFromMerchant:
            if (!string.IsNullOrEmpty(objective.description))
            {
                return objective.description;
            }
            ItemData buyItem = ItemDatabase.Instance.GetItemById(objective.targetId);
            string buyItemName = buyItem != null ? buyItem.itemName : objective.targetId;
            return $"Tüccandan satın al: {buyItemName}";
        
case QuestType.EquipItems:
    if (!string.IsNullOrEmpty(objective.description))
    {
        return objective.description;
    }
    return $"Ekipman kuşan";

// ✅ YENİ EKLEME
case QuestType.EquipUpgradedItems:
    if (!string.IsNullOrEmpty(objective.description))
    {
        return objective.description;
    }
    return "Geliştirilmiş ekipman kuşan";

default:
    if (objective.useCompass && !string.IsNullOrEmpty(objective.compassCoordinates))
            {
                if (TryParseLocationForCoordinates(objective.compassCoordinates, out Vector2 coords))
                {
                    string baseText = !string.IsNullOrEmpty(objective.description) ?
                        objective.description :
                        objective.targetId;
                    return $"{baseText} — [{coords.x:F0}, {coords.y:F0}]";
                }
            }

            return objective.targetId;
    }
}

    private bool TryParseLocationForCoordinates(string locationString, out Vector2 location)
    {
        location = Vector2.zero;

        if (string.IsNullOrEmpty(locationString))
        {
            return false;
        }

        char[] separators = { ',', ';', '|' };

        foreach (char separator in separators)
        {
            string[] parts = locationString.Split(separator);
            if (parts.Length == 2)
            {
                if (float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out float y))
                {
                    location = new Vector2(x, y);
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryParseLocationDisplay(string locationString, out string displayText)
    {
        displayText = "Belirtilen Konum";

        if (string.IsNullOrEmpty(locationString))
        {
            return false;
        }

        char[] separators = { ',', ';', '|' };

        foreach (char separator in separators)
        {
            string[] parts = locationString.Split(separator);
            if (parts.Length == 2)
            {
                if (float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out float x) &&
                    float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out float y))
                {
                    displayText = $"Konum: ({x:F1}, {y:F1})";
                    return true;
                }
            }
        }

        return false;
    }

    private void ToggleTrackerVisibility()
    {
        isTrackerVisible = !isTrackerVisible;
        questContainer.gameObject.SetActive(isTrackerVisible);

        Image panelImage = questTrackerPanel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.enabled = isTrackerVisible;
        }
    }
}