using UnityEditor;
using UnityEngine;

namespace Kuro.EditorTools
{
    // В редакторе Play-режим не замирает, когда Unity в фоне: так его можно проверять
    // через Unity MCP. В сборке игры настройка Run In Background остается выключенной.
    [InitializeOnLoad]
    static class PlayInBackground
    {
        static PlayInBackground()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode) Application.runInBackground = true;
            };
        }
    }
}
