using UnityEngine;

public class SomeMod : IMod{
	public void OnEnable(){
		Debug.Log("Mod enabled");
		UIManager.AutoAddMessage("Hello from example mod!");
	}

	public void OnDisable(){
		Debug.Log("Mod disabled");
		UIManager.AutoAddMessage("BYEEEEE D:");
	}
}