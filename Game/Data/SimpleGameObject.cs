#region

using System;
using Game.Map;
using Game.Setup;

#endregion

namespace Game.Data
{
    public abstract class SimpleGameObject
    {
        public enum Types : ushort
        {
            Troop = 100,
            Forest = 200,
        }

        public enum SystemGroupIds : uint
        {
            NewCityStartTile = 10000001,
            Forest = 10000002,
        }

        protected uint objectId;
        protected uint x;
        protected uint y;

        #region Properties

        private bool inWorld;

        private GameObjectState state = GameObjectState.NormalState();

        public bool InWorld
        {
            get
            {
                return inWorld;
            }
            set
            {
                CheckUpdateMode();
                inWorld = value;
            }
        }

        public GameObjectState State
        {
            get
            {
                return state;
            }
            set
            {
                CheckUpdateMode();
                state = value;
            }
        }

        public abstract ushort Type { get; }
        public abstract uint GroupId { get; }

        public virtual uint ObjectId
        {
            get
            {
                return objectId;
            }
            set
            {
                CheckUpdateMode();
                objectId = value;
            }
        }

        public uint X
        {
            get
            {
                return x;
            }
            set
            {
                CheckUpdateMode();
                origX = x;
                x = value;
            }
        }

        public uint Y
        {
            get
            {
                return y;
            }
            set
            {
                CheckUpdateMode();
                origY = y;
                y = value;
            }
        }

        public uint RelX
        {
            get
            {
                return x%Config.region_width;
            }
        }

        public uint RelY
        {
            get
            {
                return y%Config.region_height;
            }
        }

        public ushort CityRegionRelX
        {
            get
            {
                return (ushort)(x%Config.city_region_width);
            }
        }

        public ushort CityRegionRelY
        {
            get
            {
                return (ushort)(y%Config.city_region_height);
            }
        }

        #endregion

        #region Constructors

        protected SimpleGameObject()
        {
        }

        protected SimpleGameObject(uint x, uint y)
        {
            this.x = origX = x;
            this.y = origY = y;
        }

        #endregion

        #region Update Events

        protected uint origX;
        protected uint origY;
        protected bool updating;

        public void BeginUpdate()
        {
            if (updating)
                throw new Exception("Nesting beginupdate");
            updating = true;
            origX = x;
            origY = y;
        }

        public abstract void CheckUpdateMode();
        public abstract void EndUpdate();

        protected void Update()
        {
            if (!Global.FireEvents)
                return;

            if (updating)
                return;

            Global.World.ObjectUpdateEvent(this, origX, origY);
        }

        #endregion

        #region Methods

        public override string ToString()
        {
            return base.ToString() + "[" + X + "," + Y + "]";
        }

        internal static bool IsDiagonal(uint x, uint y, uint x1, uint y1)
        {
            return y%2 != y1%2;
        }

        public static int GetOffset(uint x, uint y, uint x1, uint y1)
        {
            if (y%2 == 1 && y1%2 == 0 && x1 <= x)
                return 1;
            if (y%2 == 0 && y1%2 == 1 && x1 >= x)
                return 1;

            return 0;
        }

        public static int TileDistance(uint x, uint y, uint x1, uint y1)
        {
            /***********************************************************
					     13,12  |  14,12 
			        12,13  |  13,13  |  14,13  |  15,13
               12,14  | (13,14) |  14,14  |  15,14  | 16,14
          11,15  |  12,15  |  13,15  |  14,15
               12,16  |  13,16  |  14,16
                    12,17  |  13,17  | 14,17
			             13,18     14,18
             *********************************************************/
            int offset = GetOffset(x, y, x1, y1);
            var dist = (int)((x1 > x ? x1 - x : x - x1) + (y1 > y ? y1 - y : y - y1)/2 + offset);

            return dist;
        }

        public int TileDistance(uint x1, uint y1)
        {
            return TileDistance(x, y, x1, y1);
        }

        public int TileDistance(SimpleGameObject obj)
        {
            return TileDistance(obj.x, obj.y);
        }

        public int RadiusDistance(uint x1, uint y1)
        {
            return RadiusDistance(x, y, x1, y1);
        }

        public int RadiusDistance(SimpleGameObject obj)
        {
            return RadiusDistance(obj.x, obj.y);
        }

        public static int RadiusDistance(uint x, uint y, uint x1, uint y1)
        {
            return RadiusLocator.Current.RadiusDistance(x, y, x1, y1);
        }

        #endregion
    }
}