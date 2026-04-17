using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SojaExiles

{
	public class opencloseDoor1 : MonoBehaviour
	{

		public Animator openandclose1;
		public bool open;
		public Transform Player;

		void Start()
		{
			open = false;
		}

		void Update()
		{
			if (!Input.GetMouseButtonDown(0))
			{
				return;
			}

			if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
			{
				return;
			}

			if (Player == null || Vector3.Distance(Player.position, transform.position) >= 15f)
			{
				return;
			}

			var camera = Camera.main ?? FindFirstObjectByType<Camera>();
			if (camera == null)
			{
				return;
			}

			var ray = camera.ScreenPointToRay(Input.mousePosition);
			if (!Physics.Raycast(ray, out var hit, Mathf.Infinity))
			{
				return;
			}

			var hitTransform = hit.collider != null ? hit.collider.transform : null;
			if (hitTransform == null || (hitTransform != transform && !hitTransform.IsChildOf(transform)))
			{
				return;
			}

			if (!open)
			{
				StartCoroutine(opening());
			}
			else
			{
				StartCoroutine(closing());
			}
		}

		IEnumerator opening()
		{
			print("you are opening the door");
			openandclose1.Play("Opening 1");
			open = true;
			yield return new WaitForSeconds(.5f);
		}

		IEnumerator closing()
		{
			print("you are closing the door");
			openandclose1.Play("Closing 1");
			open = false;
			yield return new WaitForSeconds(.5f);
		}


	}
}