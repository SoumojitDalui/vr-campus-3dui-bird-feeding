using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class fruits_tester : MonoBehaviour {

	public GameObject[] fruits;
	// Use this for initialization
	void Start () 
	{
		
	}
	
	// Update is called once per frame
	void Update () 
	{
		if (fruits == null || fruits.Length == 0) return;

		for (int i = 0; i < fruits.Length; i++) {
			GameObject fruit = fruits[i];
			if (fruit == null) continue;
			float direction = (i % 2 == 0) ? 1f : -1f;
			fruit.transform.Rotate(Vector3.up, direction * Time.deltaTime, Space.World);
		}
	}
}
