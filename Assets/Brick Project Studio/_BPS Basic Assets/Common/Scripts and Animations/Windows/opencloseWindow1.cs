using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles

{
	public class opencloseWindow1 : MonoBehaviour
	{

		public Animator openandclosewindow1;
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
			print("you are opening the Window");
			openandclosewindow1.Play("Openingwindow 1");
			open = true;
			yield return new WaitForSeconds(.5f);
		}

		IEnumerator closing()
		{
			print("you are closing the Window");
			openandclosewindow1.Play("Closingwindow 1");
			open = false;
			yield return new WaitForSeconds(.5f);
		}


	}
}