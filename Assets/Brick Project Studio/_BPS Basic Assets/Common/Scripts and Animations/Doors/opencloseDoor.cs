using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

namespace SojaExiles
{
	public class opencloseDoor : MonoBehaviour
	{

		public Animator openandclose;
		public bool open;
		public Transform Player;
		public Collider Collider;

		private void Awake()
		{
			if (Collider == null)
			{
				Collider = GetComponent<Collider>();
			}
		}

		void Update()
		{
			if (!BpsPointerRaycast.IsPrimaryClickOn(this))
			{
				return;
			}

			ApplyDoorState(!open);
		}

		private void ApplyDoorState(bool shouldOpen)
		{
			if (openandclose != null)
			{
				openandclose.Play(shouldOpen ? "Opening" : "Closing");
			}

			open = shouldOpen;
		}

		IEnumerator opening()
		{
			print("you are opening the door");
			ApplyDoorState(true);
			yield return new WaitForSeconds(.5f);
		}

		IEnumerator closing()
		{
			print("you are closing the door");
			ApplyDoorState(false);
			yield return new WaitForSeconds(.5f);
		}

		public void ToggleDoor(bool openDoor)
		{
			if (open == openDoor) return; // No change needed
			ApplyDoorState(openDoor);
		}
		
#if UNITY_EDITOR
		public void SetDoorState(bool isOpen)
		{
			open = isOpen;
			if (!EditorApplication.isPlaying)
			{
				string stateName = isOpen ? "Opening" : "Closing";
				openandclose.Rebind();
				openandclose.PlayInFixedTime(stateName, 0, 1.0f);
				openandclose.Update(0f);
			}
			else
			{
				StartCoroutine(isOpen ? opening() : closing());
			}
		}
#endif

	}
	
#if UNITY_EDITOR
	[CustomEditor(typeof(opencloseDoor))]
	public class OpenCloseDoorEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			opencloseDoor door = (opencloseDoor)target;

			GUILayout.Space(10);
			if (GUILayout.Button(door.open ? "close" : "open"))
			{
				door.SetDoorState(!door.open);
				EditorUtility.SetDirty(door.gameObject);
			}
		}
	}
#endif

}
