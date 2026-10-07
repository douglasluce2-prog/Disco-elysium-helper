using System;
using System.Collections.Concurrent;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;

namespace DiscoDictionary;

internal readonly struct DialogueLine
{
    public DialogueLine(string speaker, string text)
    {
        Speaker = speaker;
        Text = text;
    }

    public string Speaker { get; }
    public string Text { get; }
}

/// <summary>
/// Captures every line the game writes into the dialogue log (the scrolling panel on the right).
/// This is the same hook the Disco Elysium accessibility mod uses for its screen reader.
/// The patch only queues the line; all the work happens later in the mod's update loop.
/// </summary>
internal static class DialogueHook
{
    private static readonly string[] CandidateMethods = { "AddToLog", "DelayedAdd" };

    public static readonly ConcurrentQueue<DialogueLine> Pending = new();

    public static bool Installed { get; private set; }

    public static void Install(HarmonyLib.Harmony harmony, MelonLogger.Instance log)
    {
        var postfix = new HarmonyMethod(AccessTools.Method(typeof(DialogueHook), nameof(OnEntryAdded)));
        foreach (var name in CandidateMethods)
        {
            var target = AccessTools.Method(typeof(LogRenderer), name, new[] { typeof(FinalEntry) });
            if (target == null)
                continue;
            try
            {
                harmony.Patch(target, postfix: postfix);
                Installed = true;
                log.Msg($"Listening to the dialogue log (LogRenderer.{name}).");
                return;
            }
            catch (Exception ex)
            {
                log.Warning($"Could not hook LogRenderer.{name}: {ex.Message}");
            }
        }

        log.Error("Could not hook the dialogue log, so the sidebar won't fill in automatically. " +
                  "The dictionary window and the look-up key still work. (Has the game been updated?)");
    }

    // __0 = the first argument, whatever the game calls it.
    private static void OnEntryAdded(FinalEntry __0)
    {
        try
        {
            if (__0 == null)
                return;
            var text = __0.spokenLine ?? "";
            if (text.Length == 0)
                return;
            Pending.Enqueue(new DialogueLine(__0.speakerName ?? "", text));
        }
        catch (Exception)
        {
            // Never let the dictionary break the game's dialogue.
        }
    }
}
