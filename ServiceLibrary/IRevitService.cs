using System.ServiceModel;


namespace ServiceLibrary
{
    [ServiceContract]
    public interface IRevitService
    {
        [OperationContract(IsOneWay = true)]
        void SendMessage(long chatId, string message);
    }

}
