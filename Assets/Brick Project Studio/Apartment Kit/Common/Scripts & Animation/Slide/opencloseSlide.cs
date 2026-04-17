using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SojaExiles

{
	public class opencloseSlide : MonoBehaviour
	{

		public Animator openandclosewindow;
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
			openandclosewindow.Play("OpeningSlide");
			open = true;
			yield return new WaitForSeconds(.5f);
		}

		IEnumerator closing()
		{
			print("you are closing the Window");
			openandclosewindow.Play("ClosingSlide");
			open = false;
			yield return new WaitForSeconds(.5f);
		}


	}
}