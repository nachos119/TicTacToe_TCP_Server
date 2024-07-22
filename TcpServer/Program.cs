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

        private static readonly List<UserInfo> waitingClients = new List<UserInfo>();
        private static readonly object waitingClientsLockObj = new object();
        private static readonly object roomManagerLockObj = new object();
        private static readonly object clientLockObj = new object();

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
                TcpClient client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("클라이언트가 연결되었습니다.");

                UserInfo user = new UserInfo();
                user.tcpClient = client;
                user.connectNumber = connectNumber;
                lock (clientLockObj)
                {
                    clients[connectNumber] = user;
                }
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

            _ = SendPingAsync(_userInfo);

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
                        case Opcode.C_UserInfo:
                            await HandleUserInfoAsync(_userInfo, request);
                            break;
                        case Opcode.C_Create_Room:
                            await HandleCreateRoomAsync(_userInfo, request);
                            break;
                        case Opcode.C_Search_Room:
                            await HandleSearchRoomAsync(_userInfo, request);
                            break;
                        case Opcode.C_Enter_Room:
                            await HandleEnterRoomAsync(_userInfo, request);
                            break;
                        case Opcode.C_Leave_Room:
                            await HandleLeaveRoomAsync(_userInfo, request);
                            break;
                        case Opcode.C_Room_List:
                            await HandleRoomListAsync(_userInfo, request);
                            break;
                        case Opcode.C_Ready:
                            await HandleReadyAsync(_userInfo, request);
                            break;
                        case Opcode.C_Ready_Cancel:
                            await HandleReadyCancelAsync(_userInfo, request);
                            break;
                        case Opcode.C_Matching:
                            await HandleMatchingAsync(_userInfo, request);
                            break;
                        case Opcode.C_Cancel_Matching:
                            await HandleCancelMatchingAsync(_userInfo, request);
                            break;
                        case Opcode.C_TicTacToe:
                            await HandleTicTacToeAsync(_userInfo, request);
                            break;
                        case Opcode.C_Pong:
                            HandlePong(_userInfo, request);
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
                lock (clientLockObj)
                {
                    clients.Remove(_userInfo.connectNumber);
                }
            }
        }

        private static Task HandleLoginAsync(UserInfo _userInfo, string _request)
        {
            return Task.CompletedTask;
        }

        private static async Task HandleJoinRoomAsync(UserInfo _userInfo, string _request)
        {
            await Task.CompletedTask;
        }

        private static async Task HandleSendMessageAsync(UserInfo _userInfo, string _request)
        {
            await Task.CompletedTask;
        }

        private static async Task HandleReadyAsync(UserInfo _userInfo, string _request)
        {
            _userInfo.isReady = true;
            Console.WriteLine($"사용자 {_userInfo.connectNumber} 레디 상태");

            // 정보받아서
            var result = JsonConvert.DeserializeObject<RequestReady>(_request);
            Console.WriteLine($"받은 메시지: {result.opcode}");

            var room = roomManager.GetRoom(result.roomNumber);

            lock (roomManagerLockObj)
            {
                foreach (var user in room.users)
                {
                    if (user.connectNumber == _userInfo.connectNumber)
                    {
                        user.isReady = _userInfo.isReady;
                    }
                }
            }

            if (room != null && room.AllUsersReady())
            {
                var startPacket = new Packet { opcode = Opcode.C_Start };
                var convert = JsonConvert.SerializeObject(startPacket);

                foreach (var user in room.users)
                {
                    await SendResponseAsync(user, convert);
                }
                Console.WriteLine("모든 사용자가 레디 상태입니다. 게임을 시작합니다.");
            }
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
            lock (waitingClientsLockObj) // lock 문으로 대기 중인 클라이언트 목록에 대한 동시 접근 제어
            {
                waitingClients.Add(_userInfo);
            }

            if (waitingClients.Count >= 2)
            {
                UserInfo user1, user2;
                lock (waitingClientsLockObj) // lock 문으로 대기 중인 클라이언트 목록에서 클라이언트 2개를 꺼냄
                {
                    user1 = waitingClients[0];
                    user2 = waitingClients[1];
                    waitingClients.RemoveAt(0);
                    waitingClients.RemoveAt(0);

                    // 매칭 진행 상태로 설정
                    user1.isMatching = true;
                    user2.isMatching = true;
                }

                RoomInfo room;

                lock (roomManagerLockObj)
                {
                    room = roomManager.CreateRoom();
                    room.AddUser(user1);
                    room.AddUser(user2);
                    room.opcode = Opcode.C_Matching;
                }

                var convert = JsonConvert.SerializeObject(room);

                foreach (var user in room.users)
                {
                    await SendResponseAsync(user, convert);
                }

                // 매칭 완료 후 매칭 상태 해제
                user1.isMatching = false;
                user2.isMatching = false;
            }
            Console.WriteLine($"사용자 {_userInfo.connectNumber} 방 입장 대기");
        }

        private static async Task HandleTicTacToeAsync(UserInfo _userInfo, string _request)
        {
            // 정보받아서
            var result = JsonConvert.DeserializeObject<RequestGame>(_request);
            Console.WriteLine($"받은 메시지: {result.opcode}");

            var room = roomManager.GetRoom(result.roomNumber);

            room.board[result.index] = result.player; // 보드 상태 업데이트
            room.playerSelectQueue.Enqueue(result.index);


            ResponseGame responseGame = new ResponseGame
            {
                opcode = Opcode.C_TicTacToe,
                roomNumber = result.roomNumber,
                index = result.index,
                player = result.player,
                playing = true,
                delete = false
            };

            // 승리조건 검사
            int winner = CheckWin(room.board);
            if (winner != -1)
            {
                responseGame.playing = false;
                responseGame.winner = winner;
            }

            int dequeueIndex = -1;
            if (room.playerSelectQueue.Count >= 6)
            {
                dequeueIndex = room.playerSelectQueue.Dequeue();
                responseGame.delete = true;
                responseGame.deleteIndex = dequeueIndex;
                room.board[dequeueIndex] = -1;
            }

            var convert = JsonConvert.SerializeObject(responseGame);

            foreach (var user in room.users)
            {
                await SendResponseAsync(user, convert);
            }

            if (winner != -1)
            {
                lock (roomManagerLockObj)
                {
                    roomManager.RemoveRoom(result.roomNumber);
                }
            }
        }

        private static async Task HandleUserInfoAsync(UserInfo _userInfo, string _request)
        {
            RequestUserInfo requestUserInfo = new RequestUserInfo();
            requestUserInfo.opcode = Opcode.C_UserInfo;
            requestUserInfo.userInfo = _userInfo;

            var convert = JsonConvert.SerializeObject(requestUserInfo);
            await SendResponseAsync(_userInfo, convert);
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


        private static async Task HandleReadyCancelAsync(UserInfo _userInfo, string _request)
        {
            _userInfo.isReady = false;
            Console.WriteLine($"사용자 {_userInfo.connectNumber} 레디 상태");

            // 정보받아서
            var result = JsonConvert.DeserializeObject<RequestReady>(_request);
            Console.WriteLine($"받은 메시지: {result.opcode}");

            var room = roomManager.GetRoom(result.roomNumber);

            lock (roomManagerLockObj)
            {
                foreach (var user in room.users)
                {
                    if (user.connectNumber == _userInfo.connectNumber)
                    {
                        user.isReady = _userInfo.isReady;
                    }
                }
            }

            var readyCanelPacket = new Packet { opcode = Opcode.C_Ready_Cancel };
            var convert = JsonConvert.SerializeObject(readyCanelPacket);

            await SendResponseAsync(_userInfo, convert);
        }

        private static async Task HandleCreateRoomAsync(UserInfo _userInfo, string _request)
        {
            RoomInfo room;

            lock (roomManagerLockObj)
            {
                room = roomManager.CreateRoom();

                room.opcode = Opcode.C_Create_Room;
                room.AddUser(_userInfo);
            }

            var convert = JsonConvert.SerializeObject(room);
            await SendResponseAsync(_userInfo, convert);
        }

        private static async Task HandleEnterRoomAsync(UserInfo _userInfo, string _request)
        {
            // 정보받아서
            var result = JsonConvert.DeserializeObject<EnterRoom>(_request);
            Console.WriteLine($"받은 메시지: {result.opcode}");

            var room = roomManager.GetRoom(result.roominfo.roomNumber);

            room.AddUser(_userInfo);

            var convert = JsonConvert.SerializeObject(room);

            foreach (var user in room.users)
            {
                await SendResponseAsync(user, convert);
            }
        }

        private static async Task HandleLeaveRoomAsync(UserInfo _userInfo, string _request)
        {
            var result = JsonConvert.DeserializeObject<LeaveRoom>(_request);
            Console.WriteLine($"받은 메시지: {result.opcode}");

            var room = roomManager.GetRoom(result.roominfo.roomNumber);

            UserInfo opponentUser = null;
            lock (roomManagerLockObj)
            {
                room.RemoveUser(_userInfo);

                if (room.users.Count <= 0)
                {
                    roomManager.RemoveRoom(result.roominfo.roomNumber);
                }
                else
                {
                    opponentUser = room.users[0];
                }
            }

            LeaveRoom leaveRoom = new LeaveRoom();
            leaveRoom.opcode = Opcode.C_Leave_Room;
            leaveRoom.userInfo = _userInfo;

            var convert = JsonConvert.SerializeObject(leaveRoom);
            await SendResponseAsync(_userInfo, convert);

            if (opponentUser != null)
            {
                await SendResponseAsync(opponentUser, convert);
            }
        }

        private static async Task HandleSearchRoomAsync(UserInfo _userInfo, string _request)
        {
            // 정보받아서
            var result = JsonConvert.DeserializeObject<RoomInfo>(_request);
            Console.WriteLine($"받은 메시지: {result.opcode}");

            var room = roomManager.GetRoom(result.roomNumber);

            SearchRoom searchRoom = new SearchRoom();
            searchRoom.opcode = Opcode.C_Search_Room;
            searchRoom.roominfo = room;

            searchRoom.existRoom = room != null ? true : false;

            var convert = JsonConvert.SerializeObject(searchRoom);
            await SendResponseAsync(_userInfo, convert);
        }

        private static async Task HandleCancelMatchingAsync(UserInfo _userInfo, string _request)
        {
            CancelMatching cancelMatching = new CancelMatching();
            cancelMatching.opcode = Opcode.C_Cancel_Matching;

            lock (waitingClientsLockObj)
            {
                if (!_userInfo.isMatching) // 매칭 진행 중이 아닌 경우에만 매칭 취소
                {
                    waitingClients.Remove(_userInfo);
                    cancelMatching.isCancel = true;
                    Console.WriteLine($"사용자 {_userInfo.connectNumber} 매칭 취소");
                }
                else
                {
                    cancelMatching.isCancel = false;
                    Console.WriteLine($"사용자 {_userInfo.connectNumber}는 이미 매칭 중입니다.");
                }
            }

            var convert = JsonConvert.SerializeObject(cancelMatching);
            await SendResponseAsync(_userInfo, convert);
        }

        private static void HandlePong(UserInfo _userInfo, string _request)
        {
            _userInfo.hasPonged = true;
            Console.WriteLine($"핑: {_userInfo.pingTimestamp} ms");
        }

        private static async Task SendPingAsync(UserInfo _userInfo)
        {
            while (_userInfo.tcpClient.Connected)
            {
                _userInfo.pingTimestamp = DateTime.UtcNow.Ticks;
                _userInfo.hasPonged = false;

                Packet pingPacket = new Packet { opcode = Opcode.C_Ping, timestamp = DateTime.UtcNow.Ticks };

                var convert = JsonConvert.SerializeObject(pingPacket);
                await SendResponseAsync(_userInfo, convert);

                // 핑 메시지 보낸 후 일정 시간 대기
                await Task.Delay(TimeSpan.FromSeconds(10));

                // 퐁 메시지를 기다리기 위한 타임아웃
                await Task.Delay(TimeSpan.FromSeconds(5));
                if (!_userInfo.hasPonged)
                {
                    Console.WriteLine("클라이언트가 응답하지 않아 연결을 종료합니다.");
                    _userInfo.tcpClient.Close();
                    lock (clientLockObj)
                    {
                        clients.Remove(_userInfo.connectNumber);
                    }
                    break;
                }
                _userInfo.hasPonged = false; // 퐁 응답 초기화
            }
        }

    }
}