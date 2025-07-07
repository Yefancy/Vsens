using System.Collections;
using UnityEditor;
using UnityEngine;

namespace SojaExiles

{
	public class opencloseWindowApt : MonoBehaviour
	{

		public Animator openandclosewindow;
		public bool open;
		public Transform Player;

		void Start()
		{
			open = false;
		}

		void OnMouseOver()
		{
			{
				if (Player)
				{
					float dist = Vector3.Distance(Player.position, transform.position);
					if (dist < 15)
					{
						if (open == false)
						{
							if (Input.GetMouseButtonDown(0))
							{
								StartCoroutine(opening());
							}
						}
						else
						{
							if (open == true)
							{
								if (Input.GetMouseButtonDown(0))
								{
									StartCoroutine(closing());
								}
							}

						}

					}
				}

			}

		}

		IEnumerator opening()
		{
			print("you are opening the Window");
			openandclosewindow.Play("Openingwindow");
			open = true;
			yield return new WaitForSeconds(.5f);
		}

		IEnumerator closing()
		{
			print("you are closing the Window");
			openandclosewindow.Play("Closingwindow");
			open = false;
			yield return new WaitForSeconds(.5f);
		}


		public void ToggleWindow(bool openWindow)
		{
			if (open == openWindow) return; // No change needed
			StartCoroutine(open ? closing() : opening());
		}
		
#if UNITY_EDITOR
		public void SetWindowState(bool isOpen)
		{
			open = isOpen;
			if (!EditorApplication.isPlaying)
			{
				string stateName = isOpen ? "Openingwindow" : "Closingwindow";
				openandclosewindow.Rebind();
				openandclosewindow.PlayInFixedTime(stateName, 0, 1.0f);
				openandclosewindow.Update(0f);
			}
			else
			{
				StartCoroutine(isOpen ? opening() : closing());
			}
		}
#endif
	}
	
		
#if UNITY_EDITOR
	[CustomEditor(typeof(opencloseWindowApt))]
	public class OpenCloseWindowAptEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();

			opencloseWindowApt window = (opencloseWindowApt)target;

			GUILayout.Space(10);
			if (GUILayout.Button(window.open ? "close" : "open"))
			{
				window.SetWindowState(!window.open);
				EditorUtility.SetDirty(window.gameObject);
			}
		}
	}
#endif
}