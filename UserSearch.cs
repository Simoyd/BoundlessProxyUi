using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoundlessProxyUi
{
    public enum UserSearchType
    {
        SignedInt32,
        UnsignedInt32,
        SignedInt64,
        UnsignedInt64,
        SignedInt16,
        UnsignedInt16,
        SignedByte,
        UnsignedByte,
        Float,
        Double,
    }

    public class UserSearch
    {
        public UserSearchType UserSearchType { get; set; }
        public string UserValue { get; set; }

        public ObservableCollection<CommPacket> Packets { get; set; } = new ObservableCollection<CommPacket>();

        public string DisplayName => $"{UserValue} ({UserSearchType})";

        private byte[] _searchBytes = null;

        public byte[] searchBytes
        {
            get
            {
                if (_searchBytes == null)
                {
                    switch (UserSearchType)
                    {
                        case UserSearchType.SignedInt64:
                            _searchBytes = BitConverter.GetBytes(Convert.ToInt64(UserValue));
                            break;
                        case UserSearchType.UnsignedInt64:
                            _searchBytes = BitConverter.GetBytes(Convert.ToUInt64(UserValue));
                            break;
                        case UserSearchType.SignedInt32:
                            _searchBytes = BitConverter.GetBytes(Convert.ToInt32(UserValue));
                            break;
                        case UserSearchType.UnsignedInt32:
                            _searchBytes = BitConverter.GetBytes(Convert.ToUInt32(UserValue));
                            break;
                        case UserSearchType.SignedInt16:
                            _searchBytes = BitConverter.GetBytes(Convert.ToInt16(UserValue));
                            break;
                        case UserSearchType.UnsignedInt16:
                            _searchBytes = BitConverter.GetBytes(Convert.ToUInt16(UserValue));
                            break;
                        case UserSearchType.SignedByte:
                            _searchBytes = BitConverter.GetBytes(Convert.ToSByte(UserValue));
                            break;
                        case UserSearchType.UnsignedByte:
                            _searchBytes = BitConverter.GetBytes(Convert.ToByte(UserValue));
                            break;
                        case UserSearchType.Float:
                            _searchBytes = BitConverter.GetBytes((float)Convert.ToDouble(UserValue));
                            break;
                        case UserSearchType.Double:
                            _searchBytes = BitConverter.GetBytes(Convert.ToDouble(UserValue));
                            break;
                    }

                    if (_searchBytes == null)
                    {
                        _searchBytes = new byte[] { };
                    }
                }

                return _searchBytes;
            }
        }
    }
}
