using UnityEngine;

public class OtherMod : IMod{
	public void OnEnable(){
		UIManager.AutoAddMessage("Hello from Serious mod! I am not your master mmkay");
	}

	public void OnDisable(){
		UIManager.AutoAddMessage("Serious mods don't say bye, not needed");
	}
}