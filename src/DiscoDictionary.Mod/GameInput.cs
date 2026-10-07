using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DiscoDictionary;

/// <summary>
/// Stops the game from reacting to keys while the player is typing in the dictionary's search box
/// (so typing "j" doesn't open the journal). Same approach as the accessibility mod.
/// </summary>
internal static class GameInput
{
    private static bool _inControlUnavailable;

    public static void Suppress()
    {
        if (!_inControlUnavailable)
        {
            try
            {
                ClearInControl();
            }
            catch (Exception)
            {
                _inControlUnavailable = true;
            }
        }
        Input.ResetInputAxes();
    }

    // Separate method so a missing InControl type fails here, inside the try, not in the caller.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ClearInControl() => Il2CppInControl.InputManager.ClearInputState();
}
