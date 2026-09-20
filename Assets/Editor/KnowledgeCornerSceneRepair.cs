using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class KnowledgeCornerSceneRepair
{
    const string SourceScenePath = "Assets/Scenes/Multiplayer.unity";
    const string TargetScenePath = "Assets/Scenes/Singleplayer.unity";
    const string ComponentsPrefabPath = "Assets/Prefab/PC/Knowledge Corner Components.prefab";
    const string EnvironmentName = "Environment";
    const string OriginalTableName = "Table Knowledge Corner";
    const string ComponentsRootName = "Knowledge Corner Components";

    sealed class StationDefinition
    {
        public string displayName;
        public string sourceStationName;
        public string sourceGrabName;
        public string slotParentName;

        public StationDefinition(string displayName, string sourceStationName, string sourceGrabName, string slotParentName)
        {
            this.displayName = displayName;
            this.sourceStationName = sourceStationName;
            this.sourceGrabName = sourceGrabName;
            this.slotParentName = slotParentName;
        }
    }

    static readonly StationDefinition[] Stations =
    {
        new("Motherboard", "Table Knowledge Corner Motherboard", "MotherBoard (Component)", "Motherboard"),
        new("CPU", "Table Knowledge Corner CPU", "CPU", "CPU"),
        new("GPU", "Table Knowledge Corner GPU", "GPU", "GPU"),
        new("Storage", "Table Knowledge Corner Hard Drive", "SSD", "HARD DRIVE"),
        new("RAM", "Table Knowledge Corner RAM", "RAM", "RAM"),
        new("PSU", "Table Knowledge Corner PSU", "PSU", "PSU"),
        new("CPU Cooler", "Table Knowledge Corner 7", "CPU Cooler", "CPU Cooler"),
    };

    [MenuItem("TechWise 360/Repair First-Mode Knowledge Corner Components")]
    public static void Repair()
    {
        var targetScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        var targetEnvironment = FindRoot(targetScene, EnvironmentName);
        var originalTable = FindDirectChild(targetEnvironment.transform, OriginalTableName);
        ValidateOriginalTable(originalTable);

        var targetSlots = FindSlots(targetScene);
        var targetSlotPositions = Stations.ToDictionary(
            station => station.displayName,
            station => targetSlots[station.slotParentName].position);

        var sourceScene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        var sourceEnvironment = FindRoot(sourceScene, EnvironmentName);
        var sourceDisplay = FindCompleteSourceDisplay(sourceEnvironment.transform);

        var componentsRoot = new GameObject(ComponentsRootName);
        componentsRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        componentsRoot.transform.localScale = Vector3.one;

        foreach (var station in Stations)
        {
            var sourceStation = FindDirectChild(sourceDisplay.transform, station.sourceStationName);
            if (sourceStation == null)
                throw new InvalidOperationException($"Missing Multiplayer source station '{station.sourceStationName}'.");

            var sourceGrab = FindGrab(sourceStation, station.sourceGrabName);
            var targetSlotPosition = targetSlotPositions[station.displayName];
            var sourceWorldScale = sourceGrab.transform.lossyScale;
            var sourceWorldRotation = sourceGrab.transform.rotation;
            var sourceWorldHeight = sourceGrab.transform.position.y;

            var component = UnityEngine.Object.Instantiate(sourceGrab.gameObject);
            component.name = $"Knowledge Corner {station.displayName}";
            component.transform.SetParent(componentsRoot.transform, false);
            component.transform.position = new Vector3(
                targetSlotPosition.x,
                sourceWorldHeight,
                targetSlotPosition.z);
            component.transform.rotation = sourceWorldRotation;
            component.transform.localScale = sourceWorldScale;
            component.SetActive(true);
        }

        ValidateComponentsRoot(componentsRoot);
        var componentsPrefab = PrefabUtility.SaveAsPrefabAsset(componentsRoot, ComponentsPrefabPath, out var prefabSaved);
        if (!prefabSaved || componentsPrefab == null)
            throw new InvalidOperationException($"Unity could not save {ComponentsPrefabPath}.");
        UnityEngine.Object.DestroyImmediate(componentsRoot);
        AssetDatabase.SaveAssets();

        targetScene = EditorSceneManager.OpenScene(TargetScenePath, OpenSceneMode.Single);
        targetEnvironment = FindRoot(targetScene, EnvironmentName);
        originalTable = FindDirectChild(targetEnvironment.transform, OriginalTableName);
        ValidateOriginalTable(originalTable);
        RemoveExistingComponentsRoot(targetEnvironment.transform);

        var installedRoot = PrefabUtility.InstantiatePrefab(componentsPrefab, targetScene) as GameObject;
        if (installedRoot == null)
            throw new InvalidOperationException($"Unity could not instantiate {ComponentsPrefabPath}.");
        installedRoot.name = ComponentsRootName;
        installedRoot.transform.SetParent(targetEnvironment.transform, true);
        installedRoot.SetActive(true);

        ValidateOriginalTable(originalTable);
        ValidateComponentsRoot(installedRoot);
        ValidateNoDuplicateKnowledgeTables(targetEnvironment.transform);

        EditorSceneManager.MarkSceneDirty(targetScene);
        if (!EditorSceneManager.SaveScene(targetScene, TargetScenePath))
            throw new InvalidOperationException($"Unity could not save {TargetScenePath}.");

        Debug.Log("First-mode Knowledge Corner repaired: original table preserved and exactly seven components aligned to the existing circles.");
    }

    static GameObject FindRoot(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
                return root;
        }

        throw new InvalidOperationException($"Could not find root object '{objectName}' in {scene.path}.");
    }

    static GameObject FindDirectChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.TrimEnd() == childName)
                return child.gameObject;
        }

        return null;
    }

    static GameObject FindCompleteSourceDisplay(Transform environment)
    {
        foreach (Transform child in environment)
        {
            if (child.name.TrimEnd() == OriginalTableName && child.childCount == Stations.Length)
                return child.gameObject;
        }

        throw new InvalidOperationException("Could not find the complete Multiplayer Knowledge Corner source display.");
    }

    static Dictionary<string, Transform> FindSlots(Scene scene)
    {
        var slots = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (!transform.name.StartsWith("Dispenser Slot", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (transform.parent == null)
                    continue;

                slots[transform.parent.name] = transform;
            }
        }

        foreach (var station in Stations)
        {
            if (!slots.ContainsKey(station.slotParentName))
                throw new InvalidOperationException($"Missing '{station.slotParentName}' Knowledge Corner circle in {scene.path}.");
        }

        return slots;
    }

    static XRGrabInteractable FindGrab(GameObject station, string expectedName)
    {
        var matchingGrabs = station.GetComponentsInChildren<XRGrabInteractable>(true)
            .Where(grab => grab.name.Trim().StartsWith(expectedName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matchingGrabs.Length != 1)
        {
            throw new InvalidOperationException(
                $"Station '{station.name}' has {matchingGrabs.Length} grab roots matching '{expectedName}'; expected one.");
        }

        return matchingGrabs[0];
    }

    static void ValidateOriginalTable(GameObject table)
    {
        if (table == null)
            throw new InvalidOperationException("The original Singleplayer Knowledge Corner table is missing.");
        if (!table.activeSelf)
            throw new InvalidOperationException("The original Singleplayer Knowledge Corner table is inactive.");
        if (table.transform.childCount != 2)
        {
            throw new InvalidOperationException(
                $"The original table hierarchy changed: expected 2 mesh children, found {table.transform.childCount}.");
        }
    }

    static void ValidateComponentsRoot(GameObject root)
    {
        if (root == null || !root.activeSelf)
            throw new InvalidOperationException("Knowledge Corner component root is missing or inactive.");
        if (root.transform.childCount != Stations.Length)
            throw new InvalidOperationException($"Expected seven Knowledge Corner components, found {root.transform.childCount}.");

        foreach (var station in Stations)
        {
            var component = FindDirectChild(root.transform, $"Knowledge Corner {station.displayName}");
            if (component == null)
                throw new InvalidOperationException($"Missing Knowledge Corner component '{station.displayName}'.");
            if (component.GetComponentsInChildren<XRGrabInteractable>(true).Length != 1)
                throw new InvalidOperationException($"'{station.displayName}' must contain exactly one grab interactable.");
            if (!component.GetComponentsInChildren<Renderer>(true).Any(renderer => renderer.enabled))
                throw new InvalidOperationException($"'{station.displayName}' has no enabled renderer.");
        }
    }

    static void ValidateNoDuplicateKnowledgeTables(Transform environment)
    {
        var tableCount = 0;
        foreach (Transform child in environment)
        {
            if (child.name.TrimEnd() == OriginalTableName)
                tableCount++;
            if (child.name.StartsWith("Table Knowledge Corner ", StringComparison.Ordinal))
                throw new InvalidOperationException($"Duplicate station table '{child.name}' remains in Singleplayer.");
        }

        if (tableCount != 1)
            throw new InvalidOperationException($"Expected one original Knowledge Corner table, found {tableCount}.");
    }

    static void RemoveExistingComponentsRoot(Transform environment)
    {
        var existing = new List<GameObject>();
        foreach (Transform child in environment)
        {
            if (child.name == ComponentsRootName)
                existing.Add(child.gameObject);
        }

        foreach (var root in existing)
            UnityEngine.Object.DestroyImmediate(root);
    }
}
