using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace QuestTeleop
{
    public class CamRenderer : MonoBehaviour
    {
        public MeshRenderer m_QuadMeshRenderer;
        public Texture2D m_Texture2D;
        public int m_Port = 5005;

        public int m_LastSeqId = -1;
        public int m_PacketsLost = 0;

        private UdpClient m_UDPClient;
        private Thread m_ReceivingThread;
        private byte[] m_FrameBytes;
        private bool m_HasNewFrame = false;
        private readonly object m_Lock = new object();

        // Start is called before the first frame update
        void Start()
        {
            m_HasNewFrame = false;
            m_Texture2D = new Texture2D(2, 2);
            m_QuadMeshRenderer.material.mainTexture = m_Texture2D;

            m_UDPClient = new UdpClient(m_Port);
            m_ReceivingThread = new Thread(ReceiveData);
            m_ReceivingThread.IsBackground = true;
            m_ReceivingThread.Start();
        }

        private void ReceiveData()
        {
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, m_Port);
            while (true)
            {
                try
                {
                    byte[] rawData = m_UDPClient.Receive(ref anyIP);

                    // 1. Extract the ID (The first 4 bytes)
                    int currentSeqId = BitConverter.ToInt32(rawData, 0);

                    // 2. Check for Packet Loss
                    if (m_LastSeqId != -1 && currentSeqId != m_LastSeqId + 1)
                    {
                        int gap = currentSeqId - (m_LastSeqId + 1);
                        if (gap > 0) m_PacketsLost += gap;
                    }

                    // 3. Prevent "Out of Order" jitter
                    if (currentSeqId < m_LastSeqId)
                    {
                        continue; // Skip this packet; it's old news!
                    }

                    // ... rest of your copy logic ...
                    m_LastSeqId = currentSeqId;
                    // Persona Flex: Slice the 4-byte Sequence ID Header
                    // rawData[0..3] = Seq ID, rawData[4..] = JPEG Bytes
                    byte[] jpegData = new byte[rawData.Length - 4];
                    Array.Copy(rawData, 4, jpegData, 0, jpegData.Length);

                    lock (m_Lock)
                    {
                        m_FrameBytes = jpegData;
                        m_HasNewFrame = true;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"UDP Receive Error: {e.Message}");
                }
            }
        }

        void Update()
        {
            // This runs on the Main Thread (60/90/120 FPS)
            if (m_HasNewFrame)
            {
                lock (m_Lock)
                {
                    // Persona Flex: Zero-Allocation texture update
                    m_Texture2D.LoadImage(m_FrameBytes);
                    m_HasNewFrame = false;
                }
            }
        }

        private void OnApplicationQuit()
        {
            m_UDPClient?.Close();
            m_ReceivingThread?.Abort();
        }
    }
}
