using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine.XR.Content.Interaction;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public static class TechWiseSimulationModeManager
{
    public const string SimulationTypeKey = "TechWise360.SimulationType";
    public const string GameModeKey = "TechWise360.GameMode";
    public const string CompetitionIdKey = "TechWise360.CompetitionId";
    public const string CompetitionTitleKey = "TechWise360.CompetitionTitle";
    public const string AssemblyType = "assembly";
    public const string DisassemblyType = "disassembly";
    public const string TutorialMode = "tutorial";
    public const string PracticeMode = "practice";
    public const string CompetitionMode = "competition";

    static readonly FieldInfo KeychainKeysField = typeof(Keychain).GetField("m_Keys", BindingFlags.NonPublic | BindingFlags.Instance);

    public static readonly string[] AssemblyOrder =
    {
        "CPU",
        "RAM",
        "M2",
        "CPUCooler",
        "Motherboard",
        "GPU",
        "Storage",
        "PSU",
    };

    public static readonly string[] DisassemblyOrder =
    {
        "CPUCooler",
        "PSU",
        "Storage",
        "GPU",
        "M2",
        "RAM",
        "CPU",
        "Motherboard",
    };

    public static string SimulationType => PlayerPrefs.GetString(SimulationTypeKey, AssemblyType);
    public static string GameMode => PlayerPrefs.GetString(GameModeKey, PracticeMode);
    public static string CompetitionId => PlayerPrefs.GetString(CompetitionIdKey, string.Empty);
    public static string CompetitionTitle => PlayerPrefs.GetString(CompetitionTitleKey, string.Empty);
    public static bool IsAssembly => SimulationType == AssemblyType;
    public static bool IsDisassembly => SimulationType == DisassemblyType;
    public static bool IsTutorialMode => GameMode == TutorialMode;
    public static bool IsCompetitionMode => GameMode == CompetitionMode;
    public static bool IsPracticeMode => GameMode == PracticeMode;

    public static void SetMode(string simulationType, string gameMode, string competitionId = "", string competitionTitle = "")
    {
        PlayerPrefs.SetString(SimulationTypeKey, NormalizeSimulationType(simulationType));
        PlayerPrefs.SetString(GameModeKey, NormalizeGameMode(gameMode));
        PlayerPrefs.SetString(CompetitionIdKey, competitionId ?? string.Empty);
        PlayerPrefs.SetString(CompetitionTitleKey, competitionTitle ?? string.Empty);
        PlayerPrefs.Save();
    }

    public static string[] GetExpectedOrder()
    {
        return IsDisassembly ? DisassemblyOrder : AssemblyOrder;
    }

    public static int TargetSecondsForCurrentMode()
    {
        return IsDisassembly ? 420 : 600;
    }

    public static string ResolveStepId(Transform transform)
    {
        if (transform == null)
            return null;

        var keyName = GetKeySearchName(transform);
        var stepFromKeys = ResolveStepFromSearchName(keyName);
        if (!string.IsNullOrEmpty(stepFromKeys))
            return stepFromKeys;

        return ResolveStepFromSearchName(GetSearchName(transform));
    }

    static string ResolveStepFromSearchName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        if (ContainsAny(name, "thermal paste", "thermalpaste"))
            return null;

        if (ContainsAny(name, "gpuconnectersocketkey", "gpu connector", "gpu connecter"))
            return null;

        if (ContainsAny(name, "cpucoolersocketkey", "cpu cooler", "cpucooler", "cooler"))
            return "CPUCooler";
        if (ContainsAny(name, "cpusocketkey", "cpu socket", "socketcpu") || ContainsToken(name, "cpu"))
            return "CPU";
        if (ContainsAny(name, "ramsocketkey", "ram socket", "memory") || ContainsToken(name, "ram"))
            return "RAM";
        if (ContainsAny(name, "m2socketkey", "m.2", "m2 socket", "m2ssd", "m2 ssd") || ContainsToken(name, "m2"))
            return "M2";
        if (ContainsAny(name, "gpusocketkey", "graphic card", "graphics card") || ContainsToken(name, "gpu"))
            return "GPU";
        if (ContainsAny(name, "ssdsocketkey", "ssd", "hard drive", "storage"))
            return "Storage";
        if (ContainsAny(name, "psusocketkey", "power supply") || ContainsToken(name, "psu"))
            return "PSU";
        if (ContainsAny(name, "motherboardsocketkey", "motherboard"))
            return "Motherboard";

        return null;
    }

    static string NormalizeSimulationType(string value)
    {
        return string.Equals(value, DisassemblyType, StringComparison.OrdinalIgnoreCase)
            ? DisassemblyType
            : AssemblyType;
    }

    static string NormalizeGameMode(string value)
    {
        if (string.Equals(value, CompetitionMode, StringComparison.OrdinalIgnoreCase))
            return CompetitionMode;

        if (string.Equals(value, TutorialMode, StringComparison.OrdinalIgnoreCase))
            return TutorialMode;

        return PracticeMode;
    }

    static string GetSearchName(Transform transform)
    {
        var builder = new StringBuilder();
        var current = transform;
        var depth = 0;
        while (current != null && depth < 4)
        {
            builder.Append(current.name).Append(' ');
            current = current.parent;
            depth++;
        }

        AppendKeySearchName(builder, transform);

        return builder.ToString().ToLowerInvariant();
    }

    static string GetKeySearchName(Transform transform)
    {
        var builder = new StringBuilder();
        AppendKeySearchName(builder, transform);
        return builder.ToString().ToLowerInvariant();
    }

    static void AppendKeySearchName(StringBuilder builder, Transform transform)
    {
        var keychain = transform.GetComponent<Keychain>() ??
            transform.GetComponentInParent<Keychain>() ??
            transform.GetComponentInChildren<Keychain>(true);
        AppendKeychain(builder, keychain);

        var socket = transform.GetComponent<XRLockSocketInteractor>() ??
            transform.GetComponentInParent<XRLockSocketInteractor>() ??
            transform.GetComponentInChildren<XRLockSocketInteractor>(true);
        if (socket != null && socket.keychainLock != null)
        {
            foreach (var key in socket.keychainLock.requiredKeys)
            {
                if (key != null)
                    builder.Append(key.name).Append(' ');
            }
        }
    }

    static void AppendKeychain(StringBuilder builder, Keychain keychain)
    {
        if (keychain == null)
            return;

        builder.Append(keychain.name).Append(' ');
        if (KeychainKeysField?.GetValue(keychain) is not IEnumerable keys)
            return;

        foreach (var key in keys)
        {
            if (key is UnityEngine.Object unityObject && unityObject != null)
                builder.Append(unityObject.name).Append(' ');
        }
    }

    static bool ContainsAny(string value, params string[] fragments)
    {
        foreach (var fragment in fragments)
        {
            if (value.Contains(fragment))
                return true;
        }

        return false;
    }

    static bool ContainsToken(string value, string token)
    {
        var index = value.IndexOf(token, StringComparison.Ordinal);
        while (index >= 0)
        {
            var beforeBoundary = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
            var afterIndex = index + token.Length;
            var afterBoundary = afterIndex >= value.Length || !char.IsLetterOrDigit(value[afterIndex]);
            if (beforeBoundary && afterBoundary)
                return true;

            index = value.IndexOf(token, index + token.Length, StringComparison.Ordinal);
        }

        return false;
    }
}
