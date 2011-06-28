#region

using System;
using System.Collections.Generic;
using Game.Data;
using Game.Data.Tribe;
using Game.Logic;
using Game.Logic.Actions;
using Game.Setup;
using Game.Util;
using System.Linq;
#endregion

namespace Game.Comm
{
    public partial class Processor
    {
        public void CmdTribesmanSetRank(Session session, Packet packet)
        {
            uint playerId;
            byte rank;
            try
            {
                playerId = packet.GetUInt32();
                rank = packet.GetByte();
            }
            catch (Exception)
            {
                ReplyError(session, packet, Error.Unexpected);
                return;
            }

            if (session.Player.Tribesman == null)
            {
                ReplyError(session, packet, Error.TribeIsNull);
                return;
            }

            Dictionary<uint, Player> players;
            using (new MultiObjectLock(out players, playerId, session.Player.Tribesman.Tribe.Owner.PlayerId))
            {
                Tribe tribe = session.Player.Tribesman.Tribe;
                if (!tribe.IsOwner(session.Player))
                {
                    ReplyError(session, packet, Error.TribesmanNotAuthorized);
                    return;
                }

                var error = tribe.SetRank(playerId, rank);
                if (error == Error.Ok)
                    ReplySuccess(session, packet);
                else
                    ReplyError(session, packet, error);
            }
        }

        public void CmdTribesmanRequest(Session session, Packet packet)
        {
            string playerName;
            try
            {
                playerName = packet.GetString();
            }
            catch (Exception)
            {
                ReplyError(session, packet, Error.Unexpected);
                return;
            }

            if (session.Player.Tribesman == null)
            {
                ReplyError(session, packet, Error.TribeIsNull);
                return;
            }

            uint playerId;
            if (!Global.World.FindPlayerId(playerName, out playerId))
            {
                ReplyError(session, packet, Error.PlayerNotFound);
                return;
            }

            Dictionary<uint, Player> players;
            Tribe tribe = session.Player.Tribesman.Tribe;
            using (new MultiObjectLock(out players, playerId, tribe.Owner.PlayerId))
            {
                if (!tribe.HasRight(session.Player.PlayerId, "Request"))
                {
                    ReplyError(session, packet, Error.TribesmanNotAuthorized);
                    return;
                }
                if (players[playerId].Tribesman != null)
                {
                    ReplyError(session, packet, Error.TribesmanAlreadyInTribe);
                    return;
                }
                if (players[playerId].TribeRequest != 0)
                {
                    ReplyError(session, packet, Error.TribesmanPendingRequest);
                    return;
                }

                players[playerId].TribeRequest = tribe.Id;
                Global.DbManager.Save(players[playerId]);
                ReplySuccess(session, packet);
            }

        }

        public void CmdTribesmanConfirm(Session session, Packet packet)
        {
            bool isAccepting;
            try
            {
                isAccepting = packet.GetByte() == 0 ? false : true;
            }
            catch (Exception)
            {
                ReplyError(session, packet, Error.Unexpected);
                return;
            }

            Tribe tribe;

            using (new MultiObjectLock(session.Player))
            {
                if (session.Player.TribeRequest == 0)
                {
                    ReplyError(session, packet, Error.TribesmanNoRequest);
                    return;
                }

                var tribeRequestId = session.Player.TribeRequest;

                session.Player.TribeRequest = 0;
                Global.DbManager.Save(session.Player);

                if (!isAccepting)
                {
                    ReplySuccess(session, packet);
                    return;
                }

                if (!Global.Tribes.TryGetValue(tribeRequestId, out tribe))
                {
                    ReplyError(session, packet, Error.TribeNotFound);
                    return;
                }
            }

            using (new MultiObjectLock(session.Player, tribe))
            {
                Tribesman tribesman = new Tribesman(tribe, session.Player, 2);
                tribe.AddTribesman(tribesman);
                ReplySuccess(session, packet);
            }
        }

        public void CmdTribesmanAdd(Session session, Packet packet)
        {
            uint playerId;
            try
            {
                playerId = packet.GetUInt32();
            }
            catch (Exception)
            {
                ReplyError(session, packet, Error.Unexpected);
                return;
            }

            if (session.Player.Tribesman == null)
            {
                ReplyError(session, packet, Error.TribeIsNull);
                return;
            }

            Dictionary<uint, Player> players;
            using (new MultiObjectLock(out players, playerId, session.Player.Tribesman.Tribe.Owner.PlayerId))
            {
                Tribesman tribesman = new Tribesman(session.Player.Tribesman.Tribe, players[playerId], 2);
                session.Player.Tribesman.Tribe.AddTribesman(tribesman);
                ReplySuccess(session, packet);
            }
        }

        public void CmdTribesmanRemove(Session session, Packet packet)
        {
            uint playerId;
            try
            {
                playerId = packet.GetUInt32();
            }
            catch (Exception)
            {
                ReplyError(session, packet, Error.Unexpected);
                return;
            }

            if (session.Player.Tribesman == null)
            {
                ReplyError(session, packet, Error.TribeIsNull);
                return;
            }

            Dictionary<uint, Player> players;
            using (new MultiObjectLock(out players, playerId, session.Player.Tribesman.Tribe.Owner.PlayerId))
            {
                if (!players.ContainsKey(playerId))
                {
                    ReplyError(session, packet, Error.PlayerNotFound);
                    return;
                }

                Tribe tribe = session.Player.Tribesman.Tribe;
                if (!tribe.HasRight(session.Player.PlayerId, "Kick"))
                {
                    ReplyError(session, packet, Error.TribesmanNotAuthorized);
                    return;
                }
                if (tribe.IsOwner(players[playerId]))
                {
                    ReplyError(session, packet, Error.TribesmanIsOwner);
                    return;
                }
                session.Player.Tribesman.Tribe.RemoveTribesman(playerId);
                ReplySuccess(session, packet);
            }
        }

        public void CmdTribesmanUpdate(Session session, Packet packet)
        {
        }

        public void CmdTribesmanLeave(Session session, Packet packet)
        {
            if (session.Player.Tribesman == null)
            {
                ReplyError(session, packet, Error.TribeIsNull);
                return;
            }

            using (new MultiObjectLock(session.Player.Tribesman.Tribe, session.Player))
            {
                Tribe tribe = session.Player.Tribesman.Tribe;

                if (tribe.IsOwner(session.Player))
                {
                    ReplyError(session, packet, Error.TribesmanIsOwner);
                    return;
                }
                tribe.RemoveTribesman(session.Player.PlayerId);
                ReplySuccess(session, packet);
            }
        }

        public void CmdTribesmanContribute(Session session, Packet packet)
        {
            uint cityId;
            Resource resource;
            try
            {
                cityId = packet.GetUInt32();
                resource = new Resource(packet.GetInt32(), packet.GetInt32(), packet.GetInt32(), packet.GetInt32(), 0);
            }
            catch(Exception)
            {
                ReplyError(session, packet, Error.Unexpected);
                return;
            }

            if (session.Player.Tribesman == null)
            {
                ReplyError(session, packet, Error.TribeIsNull);
                return;
            }

            using (new MultiObjectLock(session.Player.Tribesman.Tribe, session.Player))
            {
                City city = session.Player.GetCity(cityId);
                Tribe tribe = session.Player.Tribesman.Tribe;

                if (city == null)
                {
                    ReplyError(session, packet, Error.Unexpected);
                    return;
                }

                if (!city.Resource.HasEnough(resource))
                {
                    ReplyError(session, packet, Error.ResourceNotEnough);
                    return;
                }

                Error error = tribe.Contribute(session.Player.PlayerId, resource);
                if (error != Error.Ok)
                {
                    ReplyError(session, packet, error);
                    return;
                }
                city.BeginUpdate();
                city.Resource.Subtract(resource);
                city.EndUpdate();
                ReplySuccess(session, packet);
            }
        }

    }
}