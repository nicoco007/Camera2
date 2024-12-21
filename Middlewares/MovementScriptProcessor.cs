﻿using Camera2.Configuration;
using Camera2.Interfaces;
using Camera2.Managers;
using Camera2.Utils;
using System.Linq;
using UnityEngine;
using static Camera2.Configuration.MovementScript;

namespace Camera2.Configuration {
	class Settings_MovementScript : CameraSubSettings {
		public string[] scriptList = new string[] { };
		public bool fromOrigin = true;
		public bool enableInMenu = false;
	}
}

namespace Camera2.Middlewares {
	class MovementScriptProcessor : CamMiddleware {
		static System.Random randomSource = new System.Random();

		Transformer scriptTransformer = null;

		MovementScript loadedScript = null;
		float currentAnimationTime = 0f;


		int frameIndex = 0;

		float lastFov = 0f;
		Vector3 lastPos = Vector3.zero;
		Quaternion lastRot = Quaternion.identity;

		float lastAnimTime = 0;

		Frame targetFrame => loadedScript.frames[frameIndex];

		private void Reset() {
			if(scriptTransformer != null) {
				scriptTransformer.position = Vector3.zero;
				scriptTransformer.rotation = Quaternion.identity;

				if(settings.MovementScript.fromOrigin)
					cam.settings.ApplyPositionAndRotation();
			}

			currentAnimationTime = 0f;
			lastAnimTime = 0;

			if(loadedScript == null)
				return;

			loadedScript = null;
			frameIndex = 0;
			lastFov = 0f;
			cam.UCamera.fieldOfView = settings.FOV;
#if DEBUG
			Plugin.Log.Info($"Resetting MovementScriptProcessor of camera {cam.name}");
#endif
		}

		public override void CamConfigReloaded() => Reset();

		public void OnDisable() => Reset();

		public override bool Pre() {
			if(settings.MovementScript.scriptList.Length == 0 ||
				(!SceneUtil.isInSong && !settings.MovementScript.enableInMenu) ||
				cam.settings.type == Configuration.CameraType.FirstPerson
			) {
				Reset();
				return true;
			}

			if(loadedScript == null) {
				var possibleScripts = settings.MovementScript.scriptList.Where(MovementScriptManager.movementScripts.ContainsKey).ToArray();

				if(possibleScripts.Length == 0)
					return true;

				var scriptToUse = possibleScripts[randomSource.Next(possibleScripts.Length)];

				loadedScript = MovementScriptManager.movementScripts[scriptToUse];

				if(loadedScript == null)
					return true;

				lastFov = cam.UCamera.fieldOfView;

				Plugin.Log.Info($"Applying Movementscript {scriptToUse} for camera {cam.name}");

				scriptTransformer ??= cam.transformchain.AddOrGet("MovementScript", TransformerOrders.MovementScriptProcessor);
			}

			if(loadedScript.syncToSong && SceneUtil.isInSong) {
				/*
				 * In MP the TimeSyncController doesnt exist in the countdown phase, 
				 * so if the script is synced to the song we'll just hardlock the time
				 * at 0 if the controller doesnt exist
				 */
				currentAnimationTime = !SceneUtil.hasSongPlayer ? 0 : SceneUtil.audioTimeSyncController.songTime;
			} else {
				currentAnimationTime += cam.timeSinceLastRender;
			}

			if(settings.MovementScript.fromOrigin) {
				cam.transformer.position = Vector3.zero;
				cam.transformer.rotation = Quaternion.identity;
			}

			if(currentAnimationTime > loadedScript.scriptDuration) {
				if(!loadedScript.loop)
					return true;

				currentAnimationTime %= loadedScript.scriptDuration;
				lastAnimTime = currentAnimationTime;
				frameIndex = 0;
			}

			for(; ; ) {
				// Rollback logic for skipping through replays
				if(lastAnimTime > currentAnimationTime) {
					while(frameIndex > 0) {
						frameIndex--;

						if(targetFrame.startTime <= currentAnimationTime)
							break;
					}
					lastAnimTime = currentAnimationTime;
				}

				if(targetFrame.startTime > currentAnimationTime)
					break;

				if(targetFrame.transitionEndTime <= currentAnimationTime) {
					lastPos = scriptTransformer.position = targetFrame.position;
					lastRot = scriptTransformer.rotation = targetFrame.rotation;
					if(targetFrame.FOV > 0)
						lastFov = cam.UCamera.fieldOfView = targetFrame.FOV;
				} else if(targetFrame.startTime <= currentAnimationTime) {
					var frameProgress = (currentAnimationTime - targetFrame.startTime) / targetFrame.duration;

					if(targetFrame.transition == MoveType.Eased)
						frameProgress = Easings.EaseInOutCubic01(frameProgress);

					scriptTransformer.position = Vector3.LerpUnclamped(lastPos, targetFrame.position, frameProgress);
					scriptTransformer.rotation = Quaternion.LerpUnclamped(lastRot, targetFrame.rotation, frameProgress);

					if(targetFrame.FOV > 0f)
						cam.UCamera.fieldOfView = Mathf.LerpUnclamped(lastFov, targetFrame.FOV, frameProgress);

					break;
				}

				if(++frameIndex >= loadedScript.frames.Count) {
					frameIndex = 0;
					break;
				}
			}

			lastAnimTime = currentAnimationTime;

			return true;
		}
	}
}