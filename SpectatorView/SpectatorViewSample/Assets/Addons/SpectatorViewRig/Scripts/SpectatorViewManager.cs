﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace SpectatorView
{
    public class SpectatorViewManager : SV_Singleton<SpectatorViewManager>
    {
#if UNITY_EDITOR
        #region DLLImports
        [DllImport("UnityCompositorInterface")]
        private static extern void GetPose(out Vector3 pos, out Quaternion rot, float UnityTimeS, int frameOffset);

        [DllImport("UnityCompositorInterface")]
        private static extern void SetSpectatorViewIP(string ip);

        [DllImport("UnityCompositorInterface")]
        private static extern bool InitializeFrameProvider();

        [DllImport("UnityCompositorInterface")]
        private static extern void UpdateCompositor();

        [DllImport("UnityCompositorInterface")]
        private static extern void StopFrameProvider();

        [DllImport("UnityCompositorInterface")]
        private static extern void ResetSV();

        [DllImport("UnityCompositorInterface")]
        private static extern void SetAudioData(byte[] audioData);

        [DllImport("UnityCompositorInterface")]
        private static extern bool IsRecording();

        [DllImport("UnityCompositorInterface")]
        private static extern void StopRecording();
        #endregion

        [Header("Visuals")]
        [Tooltip("Hologram transparency.")]
        [Range(0, 1)]
        public float Alpha = 0.95f;
        private float prevAlpha;

        // If the hologram texture moves earlier than the color texture, choose a lower value.
        // If the color texture moves earlier than the hologram texture, choose a higher value.
        // 
        // Default frame offset was found with a Canon 5D MkIII using a Blackmagic DeckLink Intensity Pro 4K or Elgato HD 60S.
        // Suggested default frame offsets for each frame provider using this camera/ capture card configuration:
        // DeckLink: 3
        // OpenCV: 6
        // Elgato: 3
        // Your frame offsets may be different depending on your camera and capture card.
        [Header("Timing")]
        [Tooltip("Number of frames of latency between camera capture and frame delivery.")]
        [Range(0, 10)]
        public int FrameOffset = 3;

        [Header("Connection")]
        [Tooltip("IP of the spectator view device.")]
        public string SpectatorViewHoloLensIP;

        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;

        [HideInInspector]
        public bool frameProviderInitialized = false;

        private void Awake()
        {
            // Remove the default audio listener, there can only be one at a time.
            if (Camera.main != null)
            {
                AudioListener listener = Camera.main.GetComponent<AudioListener>();
                if (listener != null)
                {
                    listener.enabled = false;
                }
            }
        }

        private void OnEnable()
        {
            SetSpectatorViewIP(SpectatorViewHoloLensIP.Trim());
            prevAlpha = Alpha;
        }

        private void OnValidate()
        {
            if (ShaderManager.Instance != null &&
                ShaderManager.Instance.AllShadersAndTexturesReady())
            {
                if (Alpha != prevAlpha)
                {
                    ShaderManager.Instance.alphaBlendPreviewMat.SetFloat("_Alpha", Alpha);
                }
                prevAlpha = Alpha;
            }
        }

        private void OnDestroy()
        {
            ResetCompositor();
        }

        public void ResetCompositor()
        {
            StopFrameProvider();

            if (IsRecording())
            {
                StopRecording();
            }

            if (ShaderManager.Instance != null)
            {
                ShaderManager.Instance.Reset();
            }

            ResetSV();
        }

        void Update()
        {
            GetPose(out pos, out rot, Time.time, FrameOffset);

            // Update local transform with pose data from the network.
            // Use local transform, so we can attach this object to a parent and position anywhere in our scene.
            gameObject.transform.localPosition = pos;
            gameObject.transform.localRotation = rot;

            if (!frameProviderInitialized)
            {
                frameProviderInitialized = InitializeFrameProvider();
            }

            UpdateCompositor();
        }

        // Send audio data to Compositor.
        void OnAudioFilterRead(float[] data, int channels)
        {
            Byte[] audioBytes = new Byte[data.Length * 2];

            for (int i = 0; i < data.Length; i++)
            {
                // Rescale float to short range for encoding.
                short audioEntry = (short)(data[i] * short.MaxValue);
                BitConverter.GetBytes(audioEntry).CopyTo(audioBytes, i * 2);
            }

            SetAudioData(audioBytes);
        }
#endif
    }
}
