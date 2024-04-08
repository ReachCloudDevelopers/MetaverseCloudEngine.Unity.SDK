namespace MetaverseCloudEngine.Unity.Networking.Enumerations
{
    public enum NetworkEventType : short
    {
        HostSayingGameStarted,
        HostSayingGameEnded,

        ClientSayingToSetHisPlayerGroupOnYourComputer,

        HostSayingAnotherClientWantsToSpawnYourPlayer,
        HostSayingAnotherClientWantsToDeSpawnYourPlayer,

        SomeoneSendingYouAllThePlayerGroupData,
        JoiningClientWantsAllPlayerGroupData,
        
        NetworkEventBehavior,

        PlayMakerRPC = 500
    }
}