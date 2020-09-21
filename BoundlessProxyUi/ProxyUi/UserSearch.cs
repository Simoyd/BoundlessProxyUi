using BoundlessProxyUi.ProxyUi;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace BoundlessProxyUi.ProxyUi
{
    /// <summary>
    /// Types of searches the user can perform
    /// </summary>
    enum UserSearchType
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
        Utf8String,
        Hex,
    }

    /// <summary>
    /// Object to store what the user is currently searching for
    /// </summary>
    class UserSearch
    {
        /// <summary>
        /// The type for this search
        /// </summary>
        public UserSearchType UserSearchType { get; set; }

        /// <summary>
        /// The string value entered by the user
        /// </summary>
        public string UserValue { get; set; }

        /// <summary>
        /// List of packets that match this search
        /// </summary>
        public ObservableCollection<CommPacket> Packets { get; set; } = new ObservableCollection<CommPacket>();

        /// <summary>
        /// Line to display on the UI for this search
        /// </summary>
        public string DisplayName => $"{UserValue} ({UserSearchType})";

        /// <summary>
        /// Cache for the raw bytes to search for that represents the user-entered string value
        /// </summary>
        private byte[] m_searchBytes = null;

        /// <summary>
        /// Raw bytes to search for that represents the user-entered string value
        /// </summary>
        public byte[] searchBytes
        {
            get
            {
                // If the cache hasn't been set, then set it now
                if (m_searchBytes == null)
                {
                    // Update the cached value based on the type
                    switch (UserSearchType)
                    {
                        case UserSearchType.SignedInt64:
                            m_searchBytes = BitConverter.GetBytes(Convert.ToInt64(UserValue));
                            break;
                        case UserSearchType.UnsignedInt64:
                            m_searchBytes = BitConverter.GetBytes(Convert.ToUInt64(UserValue));
                            break;
                        case UserSearchType.SignedInt32:
                            m_searchBytes = BitConverter.GetBytes(Convert.ToInt32(UserValue));
                            break;
                        case UserSearchType.UnsignedInt32:
                            m_searchBytes = BitConverter.GetBytes(Convert.ToUInt32(UserValue));
                            break;
                        case UserSearchType.SignedInt16:
                            m_searchBytes = BitConverter.GetBytes(Convert.ToInt16(UserValue));
                            break;
                        case UserSearchType.UnsignedInt16:
                            m_searchBytes = BitConverter.GetBytes(Convert.ToUInt16(UserValue));
                            break;
                        case UserSearchType.SignedByte:
                            m_searchBytes = BitConverter.GetBytes(Convert.ToSByte(UserValue));
                            break;
                        case UserSearchType.UnsignedByte:
                            m_searchBytes = BitConverter.GetBytes(Convert.ToByte(UserValue));
                            break;
                        case UserSearchType.Float:
                            m_searchBytes = BitConverter.GetBytes((float)Convert.ToDouble(UserValue));
                            break;
                        case UserSearchType.Double:
                            m_searchBytes = BitConverter.GetBytes(Convert.ToDouble(UserValue));
                            break;
                        case UserSearchType.Utf8String:
                            m_searchBytes = Encoding.UTF8.GetBytes(UserValue);
                            break;
                        case UserSearchType.Hex:
                            {
                                var onlyHex = UserValue.ToUpper().Where(cur => (cur >= '0' && cur <= '9') || (cur >= 'A' && cur <= 'Z')).ToArray();

                                var hexPairs = Enumerable.Range(0, (int)Math.Ceiling(onlyHex.Length / 2D))
                                                         .Select(cur => string.Concat(onlyHex.Skip(cur * 2).Take(2)));

                                m_searchBytes = hexPairs.Select(cur => Convert.ToByte(cur, 16)).ToArray();
                            }
                            break;
                    }

                    // Must not be null
                    if (m_searchBytes == null)
                    {
                        m_searchBytes = new byte[] { };
                    }
                }

                // return the cached value
                return m_searchBytes;
            }
        }
    }
}
