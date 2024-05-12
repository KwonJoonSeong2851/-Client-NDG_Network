using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDG
{
    /// <summary>
    /// 클라이언트 측의 콜백 인터페이스입니다.
    /// </summary>
    public interface INetPeerListener
    {

        /// <summary>
        /// 다양한 오류 조건과 주목할 만한 상황에 대한 텍스트 설명을 제공합니다.
        /// </summary>
        void DebugReturn(DebugLevel level, string message);


        /// <summary>
        /// 호출된 Response에 대한 응답을 제공하는 콜백 메서드입니다.
        /// </summary>
        /// <param name="operationResponse"></param>
        void OnOperationResponse(OperationResponse operationResponse);


        /// <summary>
        /// 비동기 작업이 완료되거나 오류가 발생했을때 게임에 알리기 위해 호출됩니다.
        /// </summary>
        /// <param name="statusCode"></param>
        void OnStatusChanged(StatusCode statusCode);

        /// <summary>
        /// 서버에서 부터 이벤트를 수신했을 때마다 호출됩니다.
        /// </summary>
        /// <param name="eventData"></param>
        void OnEvent(EventData eventData);
    }
}
