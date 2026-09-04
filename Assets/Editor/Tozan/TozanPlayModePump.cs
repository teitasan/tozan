using UnityEditor;
using UnityEngine;

namespace Tozan.Editor
{
    /// <summary>
    /// Pipeline HTTP keeps the Editor loop alive but does not tick Play Mode.
    /// If frameCount stalls, force one player step so SubScene streaming and tests can proceed.
    /// A focused Editor already ticks; stall stays 0 and this does nothing.
    /// </summary>
    [InitializeOnLoad]
    static class TozanPlayModePump
    {
        static int _lastFrame = -1;
        static int _stall;
        static bool _pumping;

        static TozanPlayModePump()
        {
            EditorApplication.update += PumpIfStalled;
        }

        static void PumpIfStalled()
        {
            if (_pumping)
                return;

            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                _stall = 0;
                _lastFrame = Time.frameCount;
                return;
            }

            int frame = Time.frameCount;
            if (frame == _lastFrame)
                _stall++;
            else
                _stall = 0;
            _lastFrame = frame;

            if (_stall < 1)
                return;

            _pumping = true;
            try
            {
                EditorApplication.isPaused = true;
                for (var i = 0; i < 4; i++)
                    EditorApplication.Step();
                EditorApplication.isPaused = false;
                _lastFrame = Time.frameCount;
                _stall = 0;
            }
            finally
            {
                _pumping = false;
            }
        }
    }
}
