﻿using UnityEngine;
using System.Collections;
using UnityEngine.Networking;

public class GunDrop : NetworkBehaviour {

	public int gunLevel = 1;
	public int gunRarity = 0;
	public bool isDefOpen = false;

	[SyncVar]
	public bool isActivated = false;
	//amount of possible parts per level
	/*int[] body = {2,2,2};
	int[] barrel = {2,2,2};
	int[] scope = {2,2,2};
	int[] stock = {2,2,2};
	int[] grip = {2,2,2};*/

	void Start (){
		

		if (isDefOpen) {
			Invoke ("lel", 0.5f);
			GetComponent<Rigidbody> ().AddTorque (Random.Range (15, 100), Random.Range (15, 100), Random.Range (15, 100));
			if (!isServer)
				GetComponent<GunBuilder> ().enabled = true;
		} else if (isActivated) {
			GunBuilder myGunBuilder = GetComponent<GunBuilder> ();
			//enable colliders
			myGunBuilder.shouldCollide = true;
			//create gun
			myGunBuilder.enabled = true;
		}


		//LeStart ();
	}


	void lel(){
		MakeGun (gunLevel, gunRarity);
	}

	public void MakeGun (GunBuilder.Gun _gun){
		GunBuilder myGunBuilder = GetComponent<GunBuilder> ();
		myGunBuilder.myGun = _gun;

		RpcMakeGun (_gun);

		//enable colliders
		myGunBuilder.shouldCollide = true;

		//create gun
		myGunBuilder.enabled = true;
	}
	
	public void MakeGun (int level, int rarity) {
		if (!isServer)
			return;

		//print ("this activated");
		gunLevel = level;
		gunRarity = rarity;

		//get my component and gun
		GunBuilder myGunBuilder = GetComponent<GunBuilder> ();
		GunBuilder.Gun myGun = myGunBuilder.myGun;

		//gun builder will handle randomising
		myGun.gunLevel = gunLevel;
		myGun.gunRarity = gunRarity;
		myGunBuilder.RandomizeGunParts ();
		myGunBuilder.SetGunParts ();

		myGun = myGunBuilder.myGun;
		RpcMakeGun (myGun);

		//randomly pick level and part
		/*myGun.bodyType = giveRandomLevelPart(body);
		myGun.barrelType = giveRandomLevelPart(barrel);
		myGun.scopeType = giveRandomLevelPart(scope);
		myGun.stockType = giveRandomLevelPart(stock);
		myGun.gripType = giveRandomLevelPart(grip);*/

		//enable colliders
		myGunBuilder.shouldCollide = true;

		//create gun
		myGunBuilder.enabled = true;

	}

	[ClientRpc]
	void RpcMakeGun (GunBuilder.Gun _gun){
		GunBuilder myGunBuilder = GetComponent<GunBuilder> ();
		myGunBuilder.myGun = _gun;

		//enable colliders
		myGunBuilder.shouldCollide = true;

		//create gun
		myGunBuilder.enabled = true;
	}

	/*int giveRandomLevelPart (int[] partType){

		int thisLevel = Random.Range (0, gunLevel);
		return Random.Range (1 + ((thisLevel) * 10), body[thisLevel] + 1 + ((thisLevel) * 10) );

	}*/
	
	// Update is called once per frame
	//void OnMouseHover () {

	
	//}

	/*public Renderer rend;
	void LeStart() {
		rend = GetComponent<Renderer>();
	}
	void OnMouseEnter() {
		rend.material.color = Color.red;
	}
	void OnMouseOver() {
		rend.material.color -= new Color(0.1F, 0, 0) * Time.deltaTime;
	}
	void OnMouseExit() {
		rend.material.color = Color.white;
	}*/
}
