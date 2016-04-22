using UnityEngine.UI;
using UnityEngine;

public class UIManager : MonoBehaviour{
	
	public Text textPrefab;
	public Transform view;

	static UIManager _instance;
	public static UIManager Instance {get{return _instance;}}

	void Awake(){
		if(_instance != null){
			// Already instantiated
			Destroy(gameObject);
			return;
		}
		_instance = this;
	}

	public void AddMessage(string message){
		Text text = Instantiate(textPrefab);
		text.text = message;
		text.gameObject.SetActive(true);
		text.transform.SetParent(view, false);
	}

	public static void AutoAddMessage(string message){
		if(_instance != null){
			_instance.AddMessage(message);
		}else{
			Debug.LogError("Tried to add message to nonexistant uimanager");
		}
	}
}