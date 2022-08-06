using Camera2.Interfaces;
using Camera2.Utils;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Camera2.Configuration {
	class Settings_FollowHead : CameraSubSettings {
		public bool enabled = false;
		public float smoothing = 2;
		[JsonConverter(typeof(Vector3Converter))] public Vector3 localSpaceOffset = new Vector3(0, 0, -0.05f);
		[JsonConverter(typeof(Vector2Converter))] public Vector2 framing = new Vector2(0.5f, 0.5f);
	}
}

namespace Camera2.Middlewares {
	class FollowHead : CamMiddleware {
		public void OnDisable() => Reset();

		Transformer rotationApplier;
		Camera camera;
		Transform cameraTransform;
		Scene lastScene;
		bool teleportOnNextFrame;

		private void Reset() {
			if(rotationApplier != null) {
				rotationApplier.rotation = Quaternion.identity;
				rotationApplier.position = Vector3.zero;
			}
		}

		public void OnEnable() {
			teleportOnNextFrame = true;
		}

		public override bool Pre() {
			if(
				!enabled ||
				!settings.FollowHead.enabled ||
				settings.type != Configuration.CameraType.Positionable
			) {
				Reset();

				return true;
			}

			if(rotationApplier == null) {
				rotationApplier = cam.transformchain.AddOrGet(nameof(FollowHead), TransformerOrders.FollowHead);
				rotationApplier.applyAsAbsolute = true;
				teleportOnNextFrame = true;
			}

			if(cameraTransform != null) {
				Vector3 targetPosition = cameraTransform.TransformPoint(settings.FollowHead.localSpaceOffset);
				float verticalFieldOfView = cam.UCamera.fieldOfView;
				float horizontalFieldOfView = Camera.VerticalToHorizontalFieldOfView(verticalFieldOfView, cam.UCamera.aspect);
				Quaternion framing = Quaternion.Euler((settings.FollowHead.framing.y - 0.5f) * verticalFieldOfView, (0.5f - settings.FollowHead.framing.x) * horizontalFieldOfView, 0);
				Quaternion targetRotation = Quaternion.LookRotation(targetPosition - cam.transformer.position) * framing;

				if(teleportOnNextFrame) {
					rotationApplier.rotation = targetRotation;
				} else {
					rotationApplier.rotation = Quaternion.Slerp(rotationApplier.rotation, targetRotation, cam.timeSinceLastRender * settings.Follow360.smoothing);
				}
			}

			teleportOnNextFrame = false;

			if(lastScene != SceneUtil.currentScene) {
				cameraTransform = null;
				lastScene = SceneUtil.currentScene;
				teleportOnNextFrame = true;
			}

			if(cameraTransform == null) {
				camera = Camera.main;

				if(camera == null) {
					return true;
				}

				cameraTransform = camera.transform;
				teleportOnNextFrame = true;
			}

			return true;
		}
	}
}