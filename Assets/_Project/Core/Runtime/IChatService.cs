namespace Game.Core
{
    /// <summary>채팅 전송의 공개 계약. 구현은 Game.Networking(PlayerChat).
    /// UI는 이 인터페이스로 "보내달라"고 요청만 하고, 실제 전파는 서버가 한다(루트 CLAUDE.md 1).
    ///
    /// 수신은 <see cref="GameplayEvents.ChatMessageReceived"/>를 구독한다
    /// (서비스 등록 시점보다 UI가 먼저 살아 있을 수 있어 정적 이벤트로 받는다).</summary>
    public interface IChatService
    {
        /// <summary>채팅 전송 요청. 서버가 검증 후 전체에게 전파한다.</summary>
        void SendChat(string message);
    }
}
