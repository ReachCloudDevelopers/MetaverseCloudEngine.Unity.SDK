using UnityEngine;
using MetaverseCloudEngine.Unity.Networking.Components;

namespace MetaverseCloudEngine.Unity.EmeraldAI
{
	[RequireComponent(typeof(NetworkTransform))]
    public class EmeraldAINetworking : NetworkObjectBehaviour
    {
	    private Component _emeraldAISystemComponent;
	    private Component _emeraldAIDetectionComponent;
	    private Component _emeraldAIInitializerComponent;
	    private Component _emeraldAIBehavioursComponent;
	    private Component _emeraldAIEventsManagerComponent;
	    private Component _emeraldAILookAtControllerComponent;
	    
	    protected override void Awake()
	    {
		    base.Awake();
		    FindComponents();
	    }

	    private void FindComponents()
	    {
		    _emeraldAISystemComponent = GetComponent("EmeraldAISystem");
		    _emeraldAIDetectionComponent = GetComponent("EmeraldAIDetection");
		    _emeraldAIInitializerComponent = GetComponent("EmeraldAIInitializer");
		    _emeraldAIBehavioursComponent = GetComponent("EmeraldAIBehaviours");
		    _emeraldAIEventsManagerComponent = GetComponent("EmeraldAIEventsManager");
		    _emeraldAILookAtControllerComponent = GetComponent("EmeraldAILookAtController");
	    }
    }
}