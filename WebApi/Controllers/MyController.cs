﻿using Entities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using WebApi.Models;
using WebApi.MyDBContext;
using Firebase.Database;
using Firebase.Database.Query;
using WebApi.ModelsFirebase;
using WebApi.ViewModels;
using System.Web;
using Newtonsoft.Json;

namespace WebApi.Controllers
{
    [RoutePrefix("api/MyApi")]
    [Authorize]
    public class MyController : ApiController
    {
        private readonly MyDbContext context;
        private readonly FirebaseClient firebaseClient;
        private static readonly string FRIEND_REQUEST = "friend_request";
        private static readonly string ROOM_REQUEST = "room_request";
        private static readonly string ROOM = "room";

        public MyController()
        {
            context = MyDbContext.Create();
            firebaseClient = new FirebaseClient("https://luu-data.firebaseio.com/");
        }

        #region User
        [HttpPost]
        [Route("UpdateInfomation")]
        public async Task<IHttpActionResult> UpdateInfomation(InfomationUerModel userModel)
        {
            ApplicationUser user = GetUserLogin();
            if (user == null)
            {
                return NotFound();
            }
            if (userModel.FullName != null)
            {
                user.FullName = userModel.FullName;
            }
            if (userModel.Address != null)
            {
                user.Address = userModel.Address;
            }
            if (userModel.DoB != null)
            {
                user.DoB = userModel.DoB;
            }
            if (userModel.Phone != null)
            {
                user.Phone = userModel.Phone;
            }
            await context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet]
        [Route("GetUserLogin")]
        public ApplicationUser GetUserLogin()
        {
            var userName = User.Identity.Name;
            var user = context.Users.FirstOrDefault(t => t.UserName.Equals(userName));
            return user;
        }

        [HttpGet]
        [Route("GetUserByEmail")]
        public ApplicationUser GetUserByEmail(String email)
        {
            var user = context.Users.FirstOrDefault(t => t.Email.Equals(email));
            return user;
        }

        [Route("UpdateAvatar")]
        [HttpGet]
        public async Task<IHttpActionResult> UpdateAvatar(string pathAvatar)
        {
            ApplicationUser user = GetUserLogin();
            user.Avatar = pathAvatar;
            await context.SaveChangesAsync();
            return Ok();
        }

        [Route("UpdateWallpaper")]
        [HttpGet]
        public async Task<IHttpActionResult> UpdateWallpaper(string pathAvatar)
        {
            ApplicationUser user = GetUserLogin();
            user.Wallpaper = pathAvatar;
            await context.SaveChangesAsync();
            return Ok();
        }

        [Route("FindUserByName")]
        [HttpGet]
        public ICollection<ApplicationUser> FindUserByName([FromUri]PagingParameterModel pagingParameterModel, string name)
        {
            var myUser = GetUserLogin();
            var friends = context.Friends
            .Where(t => t.User1Id.Equals(myUser.Id) || t.User2.Equals(myUser.Id)).ToList();

            ICollection<ApplicationUser> listUser =
            context.Users.ToList().Where(t => t.FullName.ToLower().Contains(name.ToLower()) && t.Id != myUser.Id).ToList();
            foreach( var item in listUser ) {
                var check = friends.Any(t => (t.User1.Equals(item.Id) && t.User2Id.Equals(myUser.Id)) || (t.User2Id.Equals(item.Id) && t.User1Id.Equals(myUser.Id)));
                if (check)
                {
                    item.isFriend = true;
                }
            }
            
            int count = listUser.Count();
            int CurrentPage = pagingParameterModel.pageNumber;
            int PageSize = pagingParameterModel.pageSize;
            int TotalCount = count;
            int TotalPages = (int)Math.Ceiling(count / (double)PageSize);
            var items = listUser.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
            var previousPage = CurrentPage > 1 ? "1" : "0";
            var nextPage = CurrentPage < TotalPages ? "1" : "0";
            var paginationMetadata = new
            {
                totalCount = TotalCount,
                pageSize = PageSize,
                currentPage = CurrentPage,
                totalPages = TotalPages,
                previousPage,
                nextPage
            };
            HttpContext.Current.Response.Headers.Add("Paging-Headers", JsonConvert.SerializeObject(paginationMetadata));
            return items;
        }

        [Route("GetListUser")]
        [HttpGet]
        public IList<ApplicationUser> GetListUser([FromUri]PagingParameterModel pagingparametermodel)
        {
            var myUser = GetUserLogin();
            var friends = context.Friends
            .Where(t => t.User1Id.Equals(myUser.Id) || t.User2Id.Equals(myUser.Id)).ToList();
            var list = context.Users.Where(t => !t.Id.Equals(myUser.Id) && !(friends.Any(x => x.User2Id == t.Id || x.User1Id == t.Id))).ToList();
            foreach (var item in list)
            {
                var check = friends.Any(t => (t.User1.Equals(item.Id) && t.User2Id.Equals(myUser.Id)) || (t.User2Id.Equals(item.Id) && t.User1Id.Equals(myUser.Id)));
                if (check)
                {
                    item.isFriend = true;
                }
            }

            int count = list.Count();
            int CurrentPage = pagingparametermodel.pageNumber;
            int PageSize = pagingparametermodel.pageSize;
            int TotalCount = count;
            int TotalPages = (int)Math.Ceiling(count / (double)PageSize);
            var items = list.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
            var previousPage = CurrentPage > 1 ? "1" : "0";
            var nextPage = CurrentPage < TotalPages ? "1" : "0";
            var paginationMetadata = new
            {
                totalCount = TotalCount,
                pageSize = PageSize,
                currentPage = CurrentPage,
                totalPages = TotalPages,
                previousPage,
                nextPage
            };
            HttpContext.Current.Response.Headers.Add("Paging-Headers", JsonConvert.SerializeObject(paginationMetadata));

            return items;
        }
        #endregion

        #region Friend Request
        [Route("SendFriendRequest")]
        [HttpPost]
        public async Task<IHttpActionResult> SendFriendRequest(FriendRequest friendRequest)
        {
            //kiểm tra đã gửi chưa
            var haveSend = context.FriendRequests.FirstOrDefault(t => t.UserSend.Equals(friendRequest.UserSend) && t.UserId.Equals(friendRequest.UserId));
            if (haveSend != null)
            {
                return BadRequest("already send");
            }
            //kiểm tra người kia có phải đã gửi lời mời trước rồi ko, nếu có => đồng ý
            var userSend = context.FriendRequests.FirstOrDefault(t => t.UserSend.Equals(friendRequest.UserId) && t.UserId.Equals(friendRequest.UserSend));
            if (userSend != null)
            {
                await ReplyFriendRequest(userSend.Id, true);
            }


            context.FriendRequests.Add(friendRequest);
            FriendRequestFB friendRequestFB = new FriendRequestFB()
            {
                ID = friendRequest.Id.ToString(),
                IDUser = friendRequest.UserId,
                IDUserSend = friendRequest.UserSend,
                Content = friendRequest.Content,
                TimeSend = DateTime.UtcNow.ToString("MM/dd/yyyy HH:mm:ss")
            };

            await firebaseClient
                .Child(FRIEND_REQUEST)
                .Child(friendRequest.Id.ToString())
                .PutAsync(friendRequestFB);

            await context.SaveChangesAsync();
            return Ok();
        }

        [Route("DeleteFriendRequest")]
        [HttpGet]
        public async Task<IHttpActionResult> DeleteFriendRequest([FromUri]Guid id)
        {
            var fRequest = context.FriendRequests.FirstOrDefault(t => t.Id == id);
            if (fRequest != null)
            {
                await firebaseClient.Child(FRIEND_REQUEST)
                    .Child(id.ToString())
                    .DeleteAsync();
                context.FriendRequests.Remove(fRequest);
                await context.SaveChangesAsync();
            }
            return Ok();
        }

        [Route("ReplyFriendRequest")]
        [HttpPost]
        public async Task<IHttpActionResult> ReplyFriendRequest([FromBody]Guid idFriendRequest, [FromBody] bool isAccept)
        {
            var fr = context.FriendRequests.FirstOrDefault(t => t.Id == idFriendRequest);
            if (fr == null)
            {
                return NotFound();
            }
            if (isAccept)
            {
                await AddFriend(new FriendModel() { IdUser1 = fr.UserSend, IdUser2 = fr.UserId });
            }
            await DeleteFriendRequest(fr.Id);
            await context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet]
        [Route("GetFriendRequest")]
        public FriendRequest GetFriendRequest(Guid id)
        {
            return context.FriendRequests.Find(id);
        }

        [HttpGet]
        [Route("GetAllFriendRequest")]
        public IList<FriendRequest> GetAllFriendRequest([FromUri]PagingParameterModel pagingParameterModel)
        {
            var user = GetUserLogin();
            var list = context.FriendRequests.Where(t => t.UserId.Equals(user.Id)).ToList();

            int count = list.Count();
            int CurrentPage = pagingParameterModel.pageNumber;
            int PageSize = pagingParameterModel.pageSize;
            int TotalCount = count;
            int TotalPages = (int)Math.Ceiling(count / (double)PageSize);
            var items = list.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
            var previousPage = CurrentPage > 1 ? "1" : "0";
            var nextPage = CurrentPage < TotalPages ? "1" : "0";
            var paginationMetadata = new
            {
                totalCount = TotalCount,
                pageSize = PageSize,
                currentPage = CurrentPage,
                totalPages = TotalPages,
                previousPage,
                nextPage
            };
            HttpContext.Current.Response.Headers.Add("Paging-Headers", JsonConvert.SerializeObject(paginationMetadata));
            return items;
        }
        #endregion

        #region Room
        [Route("AddRoom")]
        [HttpPost]
        public async Task<IHttpActionResult> AddRoom([FromBody] AddRoomViewModel addRoomViewModel)
        {
            Room room = new Room()
            {
                ColorRoom = "blue",
                StickerRoom = "like",
                PathAvatar = "",
                NameRoom = ""
            };
            ApplicationUser user = context.Users.Find(addRoomViewModel.UserCreate);
            if (user == null)
            {
                return BadRequest();
            }
            context.Rooms.Add(room);
            foreach (var item in addRoomViewModel.ListUserId)
            {
                var u = context.Users.Find(item);
                if (u != null)
                {
                    UserJoinRoom userJoinRoom = new UserJoinRoom()
                    {
                        Room = room,
                        User = u,
                        UserId = item
                    };
                    context.UserJoinRooms.Add(userJoinRoom);
                }
            }
            await context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        [Route("UpdateRoom")]
        public async Task<IHttpActionResult> UpdateRoom(Room room)
        {
            Room oldRoom = context.Rooms.Find(room.Id);
            if (oldRoom == null)
            {
                return NotFound();
            }
            oldRoom.KeyCall = room.KeyCall;
            oldRoom.KeyVideoCall = room.KeyVideoCall;
            oldRoom.StickerRoom = room.StickerRoom;
            oldRoom.ColorRoom = room.ColorRoom;
            await context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet]
        [Route("UpdateLastContentRoom")]
        public async Task<IHttpActionResult> UpdateLastContentRoom(Guid idRoom, string content)
        {
            Room oldRoom = context.Rooms.Find(idRoom);
            if (oldRoom == null)
            {
                return NotFound();
            }
            oldRoom.LastContent = content;
            await context.SaveChangesAsync();
            return Ok();
        }

        [HttpPost]
        [Route("RemoveUserFromRoom")]
        public async Task<IHttpActionResult> RemoveUserFromRoom([FromBody] string userId, [FromBody] Guid idRoom)
        {
            UserJoinRoom userJoinRoom = context.UserJoinRooms.Where(t => t.UserId == userId && t.Room.Id == idRoom).First();
            if (userJoinRoom != null)
            {
                context.UserJoinRooms.Remove(userJoinRoom);
            }
            await context.SaveChangesAsync();
            return Ok();
        }

        [Route("GetRoom")]
        [HttpGet]
        public Room GetRoom(Guid idRoom)
        {
            var room = context.Rooms.Find(idRoom);
            return room;
        }

        [Route("GetAllGroup")]
        [HttpGet]
        public IList<Room> GetAllGroup([FromUri]PagingParameterModel pagingParameterModel)
        {
            var user = GetUserLogin();
            var listRoom = context.Rooms.Where(t => t.IsChatGroup == true &&
            t.UserJoinRooms.Select(t1 => t1.UserId).Any(t2 => t2 == user.Id))
                .OrderBy(t => t.UpdatedDate).ToList();
            int count = listRoom.Count();
            int CurrentPage = pagingParameterModel.pageNumber;
            int PageSize = pagingParameterModel.pageSize;
            int TotalCount = count;
            int TotalPages = (int)Math.Ceiling(count / (double)PageSize);
            var items = listRoom.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
            var previousPage = CurrentPage > 1 ? "1" : "0";
            var nextPage = CurrentPage < TotalPages ? "1" : "0";
            var paginationMetadata = new
            {
                totalCount = TotalCount,
                pageSize = PageSize,
                currentPage = CurrentPage,
                totalPages = TotalPages,
                previousPage,
                nextPage
            };
            HttpContext.Current.Response.Headers.Add("Paging-Headers", JsonConvert.SerializeObject(paginationMetadata));
            return items;
        }
        #endregion

        #region Emoji
        [AllowAnonymous]
        [Route("AddEmoji")]
        [HttpGet]
        public async Task<IHttpActionResult> AddEmoji(string character, string pathEmoji, Guid idType)
        {
            if (context.Emoji.FirstOrDefault(t => t.NameEmoji.ToLower().Equals(character.ToLower())) == null)
            {
                var emoji = new Emoji
                {
                    NameEmoji = character,
                    PathImage = pathEmoji,
                    IdType = idType
                };
                context.Emoji.Add(emoji);

                await context.SaveChangesAsync();
                return Ok();
            }
            return BadRequest();
        }

        [AllowAnonymous]
        [Route("RemoveEmoji")]
        [HttpGet]
        public async Task<IHttpActionResult> RemoveEmoji(Guid id)
        {
            Emoji emoji = context.Emoji.Find(id);
            if (emoji != null)
            {
                context.Emoji.Remove(emoji);
            }
            await context.SaveChangesAsync();
            return Ok();
        }

        [AllowAnonymous]
        [Route("AddTypeEmoji")]
        [HttpGet]
        public async Task<IHttpActionResult> AddTypeEmoji(string name, string path)
        {
            if (context.TypeEmojis.FirstOrDefault(t => t.NameType.ToLower().Equals(name.ToLower())) == null)
            {
                var emoji = new TypeEmoji
                {
                    NameType = name,
                    PathThumbnail = path
                };
                context.TypeEmojis.Add(emoji);

                await context.SaveChangesAsync();
                return Ok();
            }
            return BadRequest();
        }

        [AllowAnonymous]
        [Route("RemoveTypeEmoji")]
        [HttpGet]
        public async Task<IHttpActionResult> RemoveTypeEmoji(string name)
        {
            TypeEmoji emoji = context.TypeEmojis.FirstOrDefault(t => t.NameType.ToLower().Equals(name.ToLower()));
            if (emoji != null)
            {
                context.TypeEmojis.Remove(emoji);
            }
            await context.SaveChangesAsync();
            return Ok();
        }

        [AllowAnonymous]
        [Route("GetEmoji")]
        public Emoji GetEmoji(string character)
        {
            Emoji emoji = context.Emoji.FirstOrDefault(t => t.NameEmoji.ToLower().Equals(character.ToLower()));
            return emoji;
        }

        [AllowAnonymous]
        [Route("GetAllEmoji")]
        public List<Emoji> GetAllEmoji([FromUri]PagingParameterModel pagingParameterModel)
        {
            List<Emoji> emojis = context.Emoji.ToList();
            int count = emojis.Count();

            int CurrentPage = pagingParameterModel.pageNumber;

            int PageSize = pagingParameterModel.pageSize;

            int TotalCount = count;

            int TotalPages = (int)Math.Ceiling(count / (double)PageSize);

            var items = emojis.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            var previousPage = CurrentPage > 1 ? "1" : "0";

            var nextPage = CurrentPage < TotalPages ? "1" : "0";

            var paginationMetadata = new
            {
                totalCount = TotalCount,
                pageSize = PageSize,
                currentPage = CurrentPage,
                totalPages = TotalPages,
                previousPage,
                nextPage
            };
            HttpContext.Current.Response.Headers.Add("Paging-Headers", JsonConvert.SerializeObject(paginationMetadata));

            return items;
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("GetTypeEmoji")]
        public IList<TypeEmoji> GetTypeEmoji()
        {
            return context.TypeEmojis.ToList();
        }

        [AllowAnonymous]
        [Route("GetEmojiByType")]
        [HttpGet]
        public IList<Emoji> GetEmojiByType([FromUri]PagingParameterModel pagingParameterModel, Guid id)
        {
            var list = context.Emoji.Where(t => t.IdType == id).ToList();
            int count = list.Count();

            int CurrentPage = pagingParameterModel.pageNumber;

            int PageSize = pagingParameterModel.pageSize;

            int TotalCount = count;

            int TotalPages = (int)Math.Ceiling(count / (double)PageSize);

            var items = list.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            var previousPage = CurrentPage > 1 ? "1" : "0";

            var nextPage = CurrentPage < TotalPages ? "1" : "0";

            var paginationMetadata = new
            {
                totalCount = TotalCount,
                pageSize = PageSize,
                currentPage = CurrentPage,
                totalPages = TotalPages,
                previousPage,
                nextPage
            };
            HttpContext.Current.Response.Headers.Add("Paging-Headers", JsonConvert.SerializeObject(paginationMetadata));

            return items;
        }

        #endregion

        #region Block User
        [Route("BlockUser")]
        [HttpPost]
        public async Task<IHttpActionResult> BlockUser(BlockUserModel blockUserModel)
        {
            var blockUser = context.BlockUsers.FirstOrDefault(t => t.UserID.Equals(blockUserModel.IdFromUser) && t.UserBlockId.Equals(blockUserModel.IdUserBlock));
            if (blockUser == null)
            {
                BlockUser block = new BlockUser()
                {
                    UserID = blockUserModel.IdFromUser,
                    UserBlockId = blockUserModel.IdUserBlock
                };
                context.BlockUsers.Add(block);
                await context.SaveChangesAsync();
            }
            return Ok();
        }

        [Route("RemoveBlockUser")]
        [HttpPost]
        public async Task<IHttpActionResult> RemoveBlockUser(BlockUserModel blockUserModel)
        {
            var blockUser = context.BlockUsers.FirstOrDefault(t => t.UserID.Equals(blockUserModel.IdFromUser) && t.UserBlockId.Equals(blockUserModel.IdUserBlock));
            if (blockUser != null)
            {
                context.BlockUsers.Remove(blockUser);
                await context.SaveChangesAsync();
            }
            return Ok();
        }

        [Route("GetAllBlockByIdUser")]
        [HttpGet]
        public IList<BlockUser> GetAllBlockByIdUser([FromUri]PagingParameterModel pagingParameterModel, string id)
        {
            var listBlock = context.BlockUsers.Where(t => t.UserID.Equals(id)).ToList();
            int count = listBlock.Count();

            int CurrentPage = pagingParameterModel.pageNumber;

            int PageSize = pagingParameterModel.pageSize;

            int TotalCount = count;

            int TotalPages = (int)Math.Ceiling(count / (double)PageSize);

            var items = listBlock.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            var previousPage = CurrentPage > 1 ? "1" : "0";

            var nextPage = CurrentPage < TotalPages ? "1" : "0";

            var paginationMetadata = new
            {
                totalCount = TotalCount,
                pageSize = PageSize,
                currentPage = CurrentPage,
                totalPages = TotalPages,
                previousPage,
                nextPage
            };
            HttpContext.Current.Response.Headers.Add("Paging-Headers", JsonConvert.SerializeObject(paginationMetadata));

            return items;
        }
        #endregion

        #region Friend
        [Route("AddFriend")]
        [HttpPost]
        public async Task<IHttpActionResult> AddFriend(FriendModel friendModel)
        {
            var userExist = context.Friends.FirstOrDefault(t => (t.User1Id.Equals(friendModel.IdUser1) && t.User2Id.Equals(friendModel.IdUser2))
            || (t.User1Id.Equals(friendModel.IdUser2) && t.User2Id.Equals(friendModel.IdUser1)));
            if (userExist == null)
            {
                var friend = new Friend()
                {
                    User1Id = friendModel.IdUser1,
                    User2Id = friendModel.IdUser2
                };
                context.Friends.Add(friend);

                Room room = new Room()
                {
                    ColorRoom = "blue",
                    StickerRoom = "like",
                    PathAvatar = "",
                    NameRoom = ""
                };
                context.Rooms.Add(room);
                await context.SaveChangesAsync();
                await AddUserToRoom(friendModel.IdUser1, room.Id);
                await AddUserToRoom(friendModel.IdUser2, room.Id);
            }
            return Ok();
        }

        [Route("RemoveFriend")]
        [HttpPost]
        public async Task<IHttpActionResult> RemoveFriend([FromBody] string idUser2)
        {
            var user = GetUserLogin();
            var idUser1 = user.Id;
            var friend = context.Friends
                .FirstOrDefault(t => (t.User1Id == idUser1 && t.User2Id == idUser2)
                        || (t.User1Id == idUser2 && t.User2Id == idUser1));
            if (friend == null)
            {
                return NotFound();
            }
            context.Friends.Remove(friend);
            await context.SaveChangesAsync();
            return Ok();
        }

        [Route("GetAllFriend")]
        [HttpGet]
        public IList<Friend> GetAllFriend([FromUri]PagingParameterModel pagingParameterModel)
        {
            var user = GetUserLogin();
            string idUser = user.Id;
            var friends = context.Friends
                .Where(t => t.User1Id.Equals(idUser) || t.User2.Equals(idUser)).OrderBy(t => t.UpdatedDate).ToList();
            int count = friends.Count();
            int CurrentPage = pagingParameterModel.pageNumber;
            int PageSize = pagingParameterModel.pageSize;
            int TotalCount = count;
            int TotalPages = (int)Math.Ceiling(count / (double)PageSize);
            var items = friends.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
            var previousPage = CurrentPage > 1 ? "1" : "0";
            var nextPage = CurrentPage < TotalPages ? "1" : "0";
            var paginationMetadata = new
            {
                totalCount = TotalCount,
                pageSize = PageSize,
                currentPage = CurrentPage,
                totalPages = TotalPages,
                previousPage,
                nextPage
            };
            HttpContext.Current.Response.Headers.Add("Paging-Headers", JsonConvert.SerializeObject(paginationMetadata));
            return items;
        }

        [Route("GetFriendDetail")]
        [HttpGet]
        public Friend GetFriendDetail(string idUser1, string idUser2)
        {
            var f = context.Friends.FirstOrDefault(t => (t.User1Id.Equals(idUser1) && t.User2Id.Equals(idUser2)) ||
                                                    (t.User1Id.Equals(idUser1) && t.User2Id.Equals(idUser2)));
            return f;
        }
        #endregion

        #region room request
        [HttpPost]
        [Route("SendRoomRequest")]
        public async Task<IHttpActionResult> SendRoomRequest([FromBody]string userSend, [FromBody]string user, [FromBody] string content)
        {
            var roomRq = context.RoomRequests.FirstOrDefault(t => t.UserId.Equals(user) && t.UserSendID.Equals(userSend));
            if (roomRq == null)
            {
                var rq = new RoomRequest()
                {
                    UserSendID = userSend,
                    UserId = user,
                    Content = content
                };
                context.RoomRequests.Add(rq);

                var rqbf = new RoomRequestFB()
                {
                    ID = rq.Id.ToString(),
                    IDUserSend = rq.UserSendID,
                    IDUser = rq.UserId,
                    Content = rq.Content,
                    TimeSend = DateTime.UtcNow.ToString("mm/dd/yyyy hh:mm:ss")
                };
                await firebaseClient
                    .Child(ROOM_REQUEST)
                    .Child(rq.Id.ToString())
                    .PutAsync(rqbf);
                await context.SaveChangesAsync();
            }
            return Ok();
        }

        [Route("RemoveRoomRequest")]
        [HttpGet]
        public async Task<IHttpActionResult> RemoveRoomRequest(Guid id)
        {
            var rq = context.RoomRequests.Find(id);
            if (rq != null)
            {
                context.RoomRequests.Remove(rq);
                await firebaseClient.Child(ROOM_REQUEST)
                                            .Child(rq.Id.ToString())
                                            .DeleteAsync();
                await context.SaveChangesAsync();
            }
            return Ok();
        }

        [Route("GetAllRoomRequest")]
        [HttpGet]
        public IList<RoomRequest> GetAllRoomRequest([FromUri]PagingParameterModel pagingParameterModel, string id)
        {
            var listRoomRequest = context.RoomRequests.Where(t => t.UserId.Equals(id)).ToList();
            int count = listRoomRequest.Count();

            int CurrentPage = pagingParameterModel.pageNumber;

            int PageSize = pagingParameterModel.pageSize;

            int TotalCount = count;

            int TotalPages = (int)Math.Ceiling(count / (double)PageSize);

            var items = listRoomRequest.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            var previousPage = CurrentPage > 1 ? "1" : "0";

            var nextPage = CurrentPage < TotalPages ? "1" : "0";

            var paginationMetadata = new
            {
                totalCount = TotalCount,
                pageSize = PageSize,
                currentPage = CurrentPage,
                totalPages = TotalPages,
                previousPage,
                nextPage
            };
            HttpContext.Current.Response.Headers.Add("Paging-Headers", JsonConvert.SerializeObject(paginationMetadata));

            return items;
        }
        #endregion

        #region User Join Room
        [HttpPost]
        [Route("AddUserToRoom")]
        public async Task<IHttpActionResult> AddUserToRoom([FromBody]string idUser, [FromBody] Guid idRoom)
        {
            var joinExist = context.UserJoinRooms.FirstOrDefault(t => t.UserId.Equals(idUser) && t.RoomId == idRoom);
            var user = context.Users.Find(idUser);
            if (joinExist == null)
            {
                var userJoinRoom = new UserJoinRoom()
                {
                    UserId = idUser,
                    RoomId = idRoom,
                    NickName = user.FullName,
                    LastInterractive = DateTime.Now
                };
                context.UserJoinRooms.Add(userJoinRoom);
                await context.SaveChangesAsync();
            }
            return Ok();
        }

        [HttpPost]
        [Route("RemoveUserRoom")]
        public async Task<IHttpActionResult> RemoveUserRoom([FromBody] string idUser, [FromBody] Guid idRoom)
        {
            var joinExist = context.UserJoinRooms.FirstOrDefault(t => t.UserId.Equals(idUser) && t.RoomId == idRoom);
            if (joinExist != null)
            {
                context.UserJoinRooms.Remove(joinExist);
                await context.SaveChangesAsync();
            }
            return Ok();
        }

        [Route("GetListAvatarRoom")]
        [HttpGet]
        public IList<string> GetListAvatarRoom(Guid idRoom)
        {
            var room = context.Rooms.Find(idRoom);
            var list = new List<string>();
            if (!room.PathAvatar.Equals(""))
            {
                list.Add(room.PathAvatar);
            }
            else
            {
                var listJoinRoom = context.UserJoinRooms.Where(t => t.RoomId == idRoom).Select(t => t.UserId).ToList();
                foreach (var item in listJoinRoom)
                {
                    var u = context.Users.Find(item);
                    list.Add(u.Avatar);
                }
            }
            return list;
        }

        [Route("GetListUserJoinRoom")]
        [HttpGet]
        public IList<ApplicationUser> GetListUserJoinRoom([FromUri]PagingParameterModel pagingParameterModel, Guid idRoom)
        {
            var listUser = new List<ApplicationUser>();
            var listJoinRoom = context.UserJoinRooms.Where(t => t.RoomId == idRoom).Select(t => t.UserId).ToList();
            foreach (var item in listJoinRoom)
            {
                var u = context.Users.Find(item);
                listUser.Add(u);
            }
            int count = listUser.Count();

            int CurrentPage = pagingParameterModel.pageNumber;

            int PageSize = pagingParameterModel.pageSize;

            int TotalCount = count;

            int TotalPages = (int)Math.Ceiling(count / (double)PageSize);

            var items = listUser.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            var previousPage = CurrentPage > 1 ? "1" : "0";

            var nextPage = CurrentPage < TotalPages ? "1" : "0";

            var paginationMetadata = new
            {
                totalCount = TotalCount,
                pageSize = PageSize,
                currentPage = CurrentPage,
                totalPages = TotalPages,
                previousPage,
                nextPage
            };
            HttpContext.Current.Response.Headers.Add("Paging-Headers", JsonConvert.SerializeObject(paginationMetadata));

            return items;
        }

        //[Route("GetListGroup")]
        //[HttpGet]
        //public IList<UserJoinRoom> GetListGroup([FromUri]PagingParameterModel pagingParameterModel)
        //{
        //    var user = GetUserLogin();
        //    var listRoom = context.Rooms.Where(t => t.IsChatGroup == true).Select(t => t.Id).ToList();
        //    var listJoin =
        //        context.UserJoinRooms.Where(t => t.UserId == user.Id && listRoom.Contains(t.RoomId)).OrderBy(t => t.LastInterractive).ToList();
        //    int count = listJoin.Count();
        //    int CurrentPage = pagingParameterModel.pageNumber;
        //    int PageSize = pagingParameterModel.pageSize;
        //    int TotalCount = count;
        //    int TotalPages = (int)Math.Ceiling(count / (double)PageSize);
        //    var items = listJoin.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        //    var previousPage = CurrentPage > 1 ? "1" : "0";
        //    var nextPage = CurrentPage < TotalPages ? "1" : "0";
        //    var paginationMetadata = new
        //    {
        //        totalCount = TotalCount,
        //        pageSize = PageSize,
        //        currentPage = CurrentPage,
        //        totalPages = TotalPages,
        //        previousPage,
        //        nextPage
        //    };
        //    HttpContext.Current.Response.Headers.Add("Paging-Headers", JsonConvert.SerializeObject(paginationMetadata));
        //    return items;
        //}
        #endregion

        #region Content Chat
        [HttpPost]
        [Route("SendContentChat")]
        public async Task<IHttpActionResult> SendContentChat([FromBody]ContentChatViewModel contentChatViewModel)
        {
            var contentChat = new ContentChat()
            {
                EmojiId = contentChatViewModel.EmojiId,
                RoomId = contentChatViewModel.RoomId,
                UserId = contentChatViewModel.UserId,
                TimeChat = DateTime.Now
            };

            var ccfb = new ContentChatFB()
            {
                Id = contentChat.Id.ToString(),
                ContentText = contentChatViewModel.ContentText,
                Type = contentChatViewModel.Type,
                EmojiId = contentChat.EmojiId.ToString(),
                UserId = contentChat.UserId,
                PathAudio = contentChatViewModel.PathAudio,
                PathFilde = contentChatViewModel.PathFilde,
                PathImage = contentChatViewModel.PathImage,
                PathVideo = contentChatViewModel.PathVideo,
                TimeChat = DateTime.UtcNow.ToString("MM/dd/yyyy HH:mm:ss")
            };

            await firebaseClient.Child(ROOM)
                                .Child(contentChat.RoomId.ToString())
                                .Child(contentChat.Id.ToString())
                                .PutAsync(ccfb);

            context.ContentChats.Add(contentChat);

            var listJoin = context.UserJoinRooms.Where(t => t.RoomId == contentChatViewModel.RoomId).ToList();
            foreach (var item in listJoin)
            {
                item.LastInterractive = DateTime.Now;
            }

            await context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet]
        [Route("GetMessage")]
        public async Task<IList<ContentChatViewModel>> GetMessage([FromUri]PagingParameterModel pagingParameterModel, Guid idRoom)
        {
            var listMessage = context.ContentChats.Where(t => t.RoomId == idRoom).OrderBy(t => t.TimeChat).ToList();
            int count = listMessage.Count();
            int CurrentPage = pagingParameterModel.pageNumber;
            int PageSize = pagingParameterModel.pageSize;
            int TotalCount = count;
            int TotalPages = (int)Math.Ceiling(count / (double)PageSize);
            var items = listMessage.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
            var previousPage = CurrentPage > 1 ? "1" : "0";
            var nextPage = CurrentPage < TotalPages ? "1" : "0";
            var paginationMetadata = new
            {
                totalCount = TotalCount,
                pageSize = PageSize,
                currentPage = CurrentPage,
                totalPages = TotalPages,
                previousPage,
                nextPage
            };
            HttpContext.Current.Response.Headers.Add("Paging-Headers", JsonConvert.SerializeObject(paginationMetadata));
            var result = new List<ContentChatViewModel>();
            foreach (var t in items)
            {
                var chat = await firebaseClient.Child(ROOM)
                                                            .Child(idRoom.ToString())
                                                            .Child(t.Id.ToString())
                                                            .OnceAsync<ContentChatFB>() as ContentChatFB;
                var chatModel = new ContentChatViewModel()
                {
                    EmojiId = chat.EmojiId == null ? new Guid("") : Guid.Parse(chat.EmojiId),
                    ContentText = chat.ContentText,
                    PathAudio = chat.PathAudio,
                    PathFilde = chat.PathFilde,
                    PathImage = chat.PathImage,
                    PathVideo = chat.PathVideo,
                    RoomId = idRoom,
                    Type = chat.Type,
                    UserId = chat.UserId
                };
                result.Add(chatModel);
            }

            return result;

        }
        #endregion
    }
}