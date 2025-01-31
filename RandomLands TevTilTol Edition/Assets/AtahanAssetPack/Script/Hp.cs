﻿using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class Hp : NetworkBehaviour {

	[SyncVar]
	public int hpi = 20;
	public int maxhp = 20;

	public Slider healthBar;
	[HideInInspector]
    public Slider healthBar2;

	[SyncVar]
	public int mySide = -1;

	public AudioSource impactAud;
	// Use this for initialization
	void Start () {
		hpi = maxhp;
		if(healthBar != null)
			healthBar.maxValue = maxhp;
        if (healthBar2 != null)
            healthBar2.maxValue = maxhp;
	}
	
	// Update is called once per frame
	void Update () {
		if(healthBar != null){
			healthBar.value = hpi;
			healthBar.maxValue = maxhp;
		}
        if (healthBar2 != null)
        {
			healthBar2.value = hpi;
			healthBar2.maxValue = maxhp;
        }

		//print (gameObject.name + " - " +  hpi.ToString());
	}
		
	public void Damage (int damage){
		//SetDirtyBit (0xFFFFFFFF);
		print("Backwards error keeper damage called");
	}

	public List<int> myDamagers = new List<int>();
	public int lastDamager = -1;

	public void Damage (int damage, int attackSide,int playerID, Vector3 attackPos){
		//SetDirtyBit (0xFFFFFFFF);
		if (attackSide == mySide) {
			Debug.LogWarning ("Friendly Fire!");
			attackSide = mySide - 10;
		}

		if (attackSide != mySide) {
			CmdDamage (damage,playerID, attackPos);
			if (!isServer)
				hpi -= damage;
		}
	}


	[Command]
	void CmdDamage (int damage, int playerID,Vector3 attackPos){
		
		if (lastDamager != -1) {
			myDamagers.Add (lastDamager);
		}
		lastDamager = playerID;

		hpi -= damage;
		RpcDamage (damage, attackPos);
	}

	[ClientRpc]
	void RpcDamage (int damage, Vector3 attackPos){
		if (isLocalPlayer) {
			GetComponent<PlayerRelay> ().myPlayer.GetComponent<Health> ().Damage (damage,attackPos);
			GetComponent<PlayerRelay> ().myPlayer.GetComponent<UnityStandardAssets.Characters.FirstPerson.FirstPersonController> ().GetHitStutter (damage/(float)maxhp);
			impactAud.pitch = Random.Range (0.9f, 1.1f);
			impactAud.Play ();
		}
	}


}