using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles

{
	public class opencloseStallDoor : MonoBehaviour
	{

		public Animator openandclose;
		public bool open;
		public Transform Player;

		void Start()
		{
			open = false;
		}

		void Update()
		{
			if (!BpsPointerRaycast.IsPrimaryClickOn(this, Player, 15f))
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
			openandclose.Play("OpeningStall");
			open = true;
			yield return new WaitForSeconds(.5f);
		}

		IEnumerator closing()
		{
			print("you are closing the door");
			openandclose.Play("ClosingStall");
			open = false;
			yield return new WaitForSeconds(.5f);
		}


	}
}