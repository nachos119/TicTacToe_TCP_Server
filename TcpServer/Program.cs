using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static TcpServer.Info;
using Newtonsoft.Json;

namespace TcpServer
{
    class Program
    {
        private static Dictionary<int, UserInfo> clients = new Dictionary<int, UserInfo>();
        private static int connectNumber = 0;

        private static RoomManager roomManager = new RoomManager();

        private static readonly Queue<UserInfo> waitingClients = new Queue<UserInfo>();
        private static readonly object lockObj = new object();

        private static Dictionary<int, RoomInfo> roomList = new Dictionary<int, RoomInfo>();

        // 연결 유무 확인할수있나
        private static async Task Main(string[] _args)
        {
            // TCP 리스너를 설정합니다. IP 주소와 포트를 지정합니다.
            // 5000은 임의의 포트
            TcpListener listener = new TcpListener(IPAddress.Any, 5000);
            listener.Start();
            Console.WriteLine("TCP 서버가 시작되었습니다. 클라이언트를 기다리는 중...");

            while (true)
            {
                // 클라이언트의 연결을 비동기적으로 기다립니다.
                TcpClient client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("클라이언트가 연결되었습니다.");

                UserInfo user = new UserInfo();
                user.tcpClient = client;
                user.connectNumber = connectNumber;

                clients[connectNumber] = user;
                connectNumber++;

                // 각 클라이언트 연결을 처리하는 작업을 시작합니다.
                _ = HandleClientAsync(user);
            }
        }

        private static async Task HandleClientAsync(UserInfo _userInfo)
        {
            // 네트워크 스트림을 통해 데이터 읽기와 쓰기를 처리합니다.
            NetworkStream stream = _userInfo.tcpClient.GetStream();
            byte[] buffer = new byte[1024];
            int byteCount;

            try
            {
                while ((byteCount = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    string request = Encoding.UTF8.GetString(buffer, 0, byteCount);
                    Console.WriteLine($"받은 메시지: {request}");

                    var result = JsonConvert.DeserializeObject<Packet>(request);
                    Console.WriteLine($"받은 메시지: {result.opcode}");

                    switch (result.opcode)
                    {
                        case Opcode.C_Ready:
                            await HandleReadyAsync(_userInfo, request);
                            break;
                        case Opcode.C_Start:
                            await HandleStartAsync(_userInfo, request);
                            break;
                        case Opcode.C_Room_List:
                            await HandleRoomListAsync(_userInfo, request);
                            break;
                        case Opcode.C_Matching:
                            await HandleMatchingAsync(_userInfo, request);
                            break;
                        case Opcode.C_UserInfo:
                            await HandleUserInfoAsync(_userInfo, request);
                            break;
                        case Opcode.C_TicTacToe:
                            await HandleTicTacToeAsync(_userInfo, request);
                            break;
                        case Opcode.C_Ping:
                            await HandlePingAsync(_userInfo, request);
                            break;
                        default:
                            Console.WriteLine("알 수 없는 Opcode입니다.");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"클라이언트 처리 중 오류 발생: {ex.Message}");
            }
            finally
            {
                // 클라이언트 연결을 종료합니다.
                _userInfo.tcpClient.Close();
                clients.Remove(_userInfo.connectNumber);
            }
        }

        #region oldCode
        private static Task HandleLoginAsync(UserInfo _userInfo, string _request)
        {
            Console.WriteLine($"사용자 {_userInfo.connectNumber} 로그인: {_request}");
            return Task.CompletedTask; // 비동기 작업이 없으므로 CompletedTask 반환
        }

        private static async Task HandleCreateRoomAsync(UserInfo _userInfo, string _request)
        {
            // 정보받아서
            var result = JsonConvert.DeserializeObject<Packet>(_request);
            Console.WriteLine($"받은 메시지: {result.opcode}");

            RoomInfo createRoom = roomManager.CreateRoom();
            var convert = JsonConvert.SerializeObject(createRoom);
            await SendResponseAsync(_userInfo, convert);
            Console.WriteLine($"사용자 {_userInfo.connectNumber} 방 추가: {_request}");
        }

        private static async Task HandleJoinRoomAsync(UserInfo _userInfo, string _request)
        {
            // 정보받아서
            var result = JsonConvert.DeserializeObject<Packet>(_request);
            Console.WriteLine($"받은 메시지: {result.opcode}");

            var convert = JsonConvert.SerializeObject(result);
            await SendResponseAsync(_userInfo, convert);
            Console.WriteLine($"사용자 {_userInfo.connectNumber} 방 확인: {_request}");
        }

        private static async Task HandleSearchRoomAsync(UserInfo _userInfo, string _request)
        {
            // 정보받아서
            var result = JsonConvert.DeserializeObject<Packet>(_request);
            Console.WriteLine($"받은 메시지: {result.opcode}");

            // 방찾기로 바꿔야함
            var roomList = roomManager.GetRoomList();
            var convert = JsonConvert.SerializeObject(roomList);
            await SendResponseAsync(_userInfo, convert);
            Console.WriteLine($"사용자 {_userInfo.connectNumber} 방 검색: {_request}");
        }

        private static async Task HandleSendMessageAsync(UserInfo _userInfo, string _request)
        {
            // 정보받아서
            var result = JsonConvert.DeserializeObject<Packet>(_request);
            Console.WriteLine($"받은 메시지: {result.opcode}");

            Console.WriteLine($"사용자 {_userInfo.connectNumber} 메시지 전송: {_request}");
            string response = $"서버 응답: {_request}";
            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await _userInfo.tcpClient.GetStream().WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private static Task HandleRoomEntryAsync(UserInfo _userInfo, string _request)
        {
            Console.WriteLine($"사용자 여긴가");
            Console.WriteLine($"사용자 {_userInfo.connectNumber} 방 입장: {_request}");
            return Task.CompletedTask; // 비동기 작업이 없으므로 CompletedTask 반환
        }

        #endregion

        private static Task HandleReadyAsync(UserInfo _userInfo, string _request)
        {
            Console.WriteLine($"사용자 {_userInfo.connectNumber} 준비 완료: {_request}");
            return Task.CompletedTask; // 비동기 작업이 없으므로 CompletedTask 반환
        }

        private static Task HandleStartAsync(UserInfo _userInfo, string _request)
        {
            Console.WriteLine($"사용자 {_userInfo.connectNumber} 게임 시작: {_request}");
            return Task.CompletedTask; // 비동기 작업이 없으므로 CompletedTask 반환
        }

        private static async Task HandleRoomListAsync(UserInfo _userInfo, string _request)
        {
            RequestRoomList requestRoomList = new RequestRoomList();
            requestRoomList.opcode = Opcode.C_Room_List;
            requestRoomList.roomList = roomManager.GetRoomList();
            var convert = JsonConvert.SerializeObject(requestRoomList);
            await SendResponseAsync(_userInfo, convert);
            Console.WriteLine($"사용자 {_userInfo.connectNumber} 방 목록 요청: {_request}");
        }

        private static async Task HandleMatchingAsync(UserInfo _userInfo, string _request)
        {
            lock (lockObj) // lock 문으로 대기 중인 클라이언트 목록에 대한 동시 접근 제어
            {
                waitingClients.Enqueue(_userInfo);
            }

            if (waitingClients.Count >= 2)
            {
                UserInfo user1, user2;
                lock (lockObj) // lock 문으로 대기 중인 클라이언트 목록에서 클라이언트 2개를 꺼냄
                {
                    user1 = waitingClients.Dequeue();
                    user2 = waitingClients.Dequeue();
                }

                RoomInfo roomInfo = new RoomInfo(100);
                roomInfo.users.Add(user1);
                roomInfo.users.Add(user2);

                roomInfo.opcode = Opcode.C_Matching;

                var convert = JsonConvert.SerializeObject(roomInfo);
                byte[] responseBytes = Encoding.UTF8.GetBytes(convert);
                await user1.tcpClient.GetStream().WriteAsync(responseBytes, 0, responseBytes.Length);
                await user2.tcpClient.GetStream().WriteAsync(responseBytes, 0, responseBytes.Length);
                roomList.Add(100, roomInfo);

                Console.WriteLine($"사용자 {roomList[100].users[0]} 방 입장");
                Console.WriteLine($"사용자 {roomList[100].users[1]} 방 입장");
            }
            Console.WriteLine($"사용자 {_userInfo.connectNumber} 방 입장 대기");
        }

        private static async Task HandleTicTacToeAsync(UserInfo _userInfo, string _request)
        {
            // 정보받아서
            var result = JsonConvert.DeserializeObject<RequestGame>(_request);
            Console.WriteLine($"받은 메시지: {result.opcode}");

            var room = roomList[result.roomNumber];
            var user1 = room.users[0];
            var user2 = room.users[1];

            room.board[result.index] = result.player; // 보드 상태 업데이트
            room.playerSelectQueue.Enqueue(result.index);

            int dequeueIndex = -1;
            if (room.playerSelectQueue.Count > 5)
            {
                dequeueIndex = room.playerSelectQueue.Dequeue();
            }

            ResponseGame responseGame = new ResponseGame
            {
                opcode = Opcode.C_TicTacToe,
                roomNumber = result.roomNumber,
                index = result.index,
                player = result.player,
                playing = true,
                delete = false
            };


            if (dequeueIndex != -1)
            {
                responseGame.delete = true;
                responseGame.deleteIndex = dequeueIndex;
                room.board[dequeueIndex] = -1;
            }

            // 승리조건 검사
            int winner = CheckWin(room.board);
            if (winner != -1)
            {
                responseGame.playing = false;
                responseGame.winner = winner;
            }

            var convert = JsonConvert.SerializeObject(responseGame);
            byte[] responseBytes = Encoding.UTF8.GetBytes(convert);
            await user1.tcpClient.GetStream().WriteAsync(responseBytes, 0, responseBytes.Length);
            await user2.tcpClient.GetStream().WriteAsync(responseBytes, 0, responseBytes.Length);

            if (winner != -1)
            {
                roomList.Remove(result.roomNumber);
            }
        }

        private static async Task HandleUserInfoAsync(UserInfo _userInfo, string _request)
        {
            RequestUserInfo requestUserInfo = new RequestUserInfo();
            requestUserInfo.opcode = Opcode.C_UserInfo;
            requestUserInfo.userInfo = _userInfo;

            var convert = JsonConvert.SerializeObject(requestUserInfo);
            byte[] responseBytes = Encoding.UTF8.GetBytes(convert);
            await _userInfo.tcpClient.GetStream().WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private static async Task SendResponseAsync(UserInfo _userInfo, string response)
        {
            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await _userInfo.tcpClient.GetStream().WriteAsync(responseBytes, 0, responseBytes.Length);
        }

        private static int CheckWin(int[] board)
        {
            int[][] winPatterns = new int[][]
            {
                new int[] { 0, 1, 2 }, // 첫 번째 행
                new int[] { 3, 4, 5 }, // 두 번째 행
                new int[] { 6, 7, 8 }, // 세 번째 행
                new int[] { 0, 3, 6 }, // 첫 번째 열
                new int[] { 1, 4, 7 }, // 두 번째 열
                new int[] { 2, 5, 8 }, // 세 번째 열
                new int[] { 0, 4, 8 }, // 대각선 \
                new int[] { 2, 4, 6 }  // 대각선 /
            };

            foreach (var pattern in winPatterns)
            {
                if (board[pattern[0]] != -1 &&
                    board[pattern[0]] == board[pattern[1]] &&
                    board[pattern[1]] == board[pattern[2]])
                {
                    return board[pattern[0]]; // 승리한 플레이어 반환
                }
            }

            return -1; // 승리한 플레이어가 없으면 -1 반환
        }

        private static async Task HandlePingAsync(UserInfo _userInfo, string _request)
        {
            // 클라이언트에서 받은 Ping 메시지에 대한 Pong 응답을 보냅니다.
            var pongResponse = new Packet { opcode = Opcode.C_Pong };
            var responseJson = JsonConvert.SerializeObject(pongResponse);
            await SendResponseAsync(_userInfo, responseJson);
        }
    }
}
