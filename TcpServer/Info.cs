using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace TcpServer
{
    class Info
    {
        public enum Opcode // : byte
        {
            C_Login = 0,
            C_Create_Room = 1,
            C_Join_Room = 2,
            C_Search_Room = 3,
            C_Ready = 4,
            C_Start = 5,
            C_Room_List = 6,
            C_Room_Entry = 7,
            C_Matching = 8,
            C_Cancle_Matching = 9,

            SendMessage = 10,

            C_UserInfo = 14,

            C_TicTacToe = 20,

            C_Ping = 50,
            C_Pong = 51,
        }

        public class Packet
        {
            public Opcode opcode;
            public string message;
        }

        public class UserInfo
        {
            [JsonIgnore]
            public TcpClient tcpClient { get; set; }

            public string name { get; set; }
            public RoomInfo currentRoom { get; set; }

            // 연결된 번호
            public int connectNumber { get; set; }
        }

        public class RoomInfo : Packet
        {
            public int roomNumber { get; set; }
            public List<UserInfo> users { get; set; }
            public bool isPlaying { get; set; }
            public int[] board { get; set; } // 보드 상태 저장
            public Queue<int> playerSelectQueue { get; set; }

            public RoomInfo(int _roomNumber)
            {
                roomNumber = _roomNumber;
                users = new List<UserInfo>();
                isPlaying = false;
                playerSelectQueue = new Queue<int>();

                int count = 9; // 틱택토 보드는 3x3 크기
                board = new int[count];
                for (int i = 0; i < count; i++)
                {
                    board[i] = -1; // 빈 칸은 -1로 초기화
                }
            }

            public void AddUser(UserInfo _user)
            {
                users.Add(_user);
            }

            public void RemoveUser(UserInfo _user)
            {
                users.Remove(_user);
            }

            public void StartGame()
            {
                isPlaying = true;
            }

            public void EndGame()
            {
                isPlaying = false;
            }
        }

        public class RoomManager
        {
            private Dictionary<int, RoomInfo> rooms;
            private int nextRoomNumber;

            public RoomManager()
            {
                rooms = new Dictionary<int, RoomInfo>();
                nextRoomNumber = 1;
            }

            public RoomInfo CreateRoom()
            {
                int roomNumber = nextRoomNumber++;
                RoomInfo room = new RoomInfo(roomNumber);
                rooms[roomNumber] = room;
                return room;
            }

            public RoomInfo GetRoom(int _roomNumber)
            {
                rooms.TryGetValue(_roomNumber, out RoomInfo room);
                return room;
            }

            public bool RemoveRoom(int _roomNumber)
            {
                return rooms.Remove(_roomNumber);
            }

            public List<RoomInfo> GetRoomList()
            {
                return new List<RoomInfo>(rooms.Values);
            }
        }

        public class RequestGame : Packet
        {
            public int roomNumber { get; set; }
            public int index { get; set; }
            public int player { get; set; } // 플레이어 번호 추가
        }

        public class ResponseGame : Packet
        {
            public int roomNumber { get; set; }
            public int index { get; set; }
            public int player { get; set; } // 플레이어 번호 추가
            public bool playing { get; set; }
            public int winner { get; set; } = -1; // 초기값 -1로 설정
            public bool delete { get; set; }
            public int deleteIndex { get; set; } = -1; // 초기값 -1로 설정
        }

        public class RequestRoomList : Packet
        {
            public List<RoomInfo> roomList { get; set; }
        }

        public class RequestUserInfo : Packet
        {
            public UserInfo userInfo { get; set; }
        }
    }
}
