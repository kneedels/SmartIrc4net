/**
 * $Id: IrcConnection.cs,v 1.1 2003/11/16 16:58:42 meebey Exp $
 * $Revision: 1.1 $
 * $Author: meebey $
 * $Date: 2003/11/16 16:58:42 $
 *
 * Copyright (c) 2003 Mirco 'meebey' Bauer <mail@meebey.net> <http://www.meebey.net>
 * 
 * Full LGPL License: <http://www.gnu.org/licenses/lgpl.txt>
 * 
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 2.1 of the License, or (at your option) any later version.
 *
 * This library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public
 * License along with this library; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

using System;
using System.IO;
using System.Text;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Specialized;
using System.Threading;

namespace SmartIRC
{
    public delegate void ReadLineEventHandler(string data);
    public delegate void WriteLineEventHandler(string data);
    public delegate void ConnectingEventHandler();
    public delegate void ConnectEventHandler();
    public delegate void DisconnectEventHandler();

    public class Connection
    {
        private IrcTcpClient        _TcpClient = new IrcTcpClient();
        private StreamReader        _Reader;
        private StreamWriter        _Writer;
        private string              _Address;
        private int                 _Port;
        private SortedList          _SendBuffer = new SortedList();
        private MemoryStream        _ThreadReadBuffer = new MemoryStream();
        private StreamReader        _ThreadReadBufferStreamReader;
        private StreamWriter        _ThreadReadBufferStreamWriter;
        public  Encoding            Encoding = Encoding.GetEncoding(1250);

        public event ReadLineEventHandler   OnReadLine;
        public event WriteLineEventHandler  OnWriteLine;
        public event ConnectingEventHandler OnConnecting;
        public event ConnectEventHandler    OnConnect;
        public event DisconnectEventHandler OnDisconnect;

        public bool Connected
        {
            get {
                return _TcpClient.Socket.Connected;
            }
        }

        public Connection()
        {
            _ThreadReadBufferStreamReader = new StreamReader(_ThreadReadBuffer, Encoding);
            _ThreadReadBufferStreamWriter = new StreamWriter(_ThreadReadBuffer, Encoding);

            _SendBuffer[Priority.Low]     = new StringCollection();
            _SendBuffer[Priority.Medium]  = new StringCollection();
            _SendBuffer[Priority.High]    = new StringCollection();
        }

        public bool Connect(string address, int port)
        {
            Logger.Connection.Info("connecting");
            _Address = address;
            _Port = port;

            if (OnConnecting != null) {
                OnConnecting();
            }
            try {
                System.Net.IPAddress ip = System.Net.Dns.Resolve(address).AddressList[0];
                _TcpClient.Connect(ip, port);
                if (OnConnect != null) {
                    OnConnect();
                }

                _Reader = new StreamReader(_TcpClient.GetStream(), Encoding);
                _Writer = new StreamWriter(_TcpClient.GetStream(), Encoding);
                Logger.Connection.Info("connected");
                return true;
            } catch {
                return false;
            }
        }

        public bool Disconnect()
        {
            if (Connected == true) {
                _Reader.Close();
                _Writer.Close();
                _TcpClient.Close();

                _ThreadReadBufferStreamReader.Close();
                _ThreadReadBufferStreamWriter.Close();
                _ThreadReadBuffer.Close();
                if (OnDisconnect != null) {
                    OnDisconnect();
                }
                Logger.Connection.Info("disconnected");
                return true;
            } else {
                return false;
            }
        }

        public void Listen()
        {
            Thread t = new Thread(new ThreadStart(_ReadThread));
            t.Start();
            while((Connected == true) &&
                  (ReadLine() != null)) {
                  // ReadLine does the work...
            }
        }

        public void ListenOnce()
        {
            ReadLine();
        }

        public string ReadLine()
        {
            string data = "";
            while ((Connected == true) &&
                   (data = _ThreadReadBufferStreamReader.ReadLine()) == null) {
                Thread.Sleep(100);
            }

            if (data != null) {
                Logger.Socket.Debug("buffer read: \""+data+"\"");
                if (OnReadLine != null) {
                    OnReadLine(data);
                }
            }

            return data;
        }

        public void WriteLine(string data, Priority priority)
        {
            _WriteLine(data, priority);
        }

        public void WriteLine(string data)
        {
            _WriteLine(data, Priority.Medium);
        }

        private void _WriteLine(string data, Priority priority)
        {
            _Writer.WriteLine(data);
            _Writer.Flush();
            Logger.Socket.Debug("sent: \""+data+"\"");
            if (OnWriteLine != null) {
                OnWriteLine(data);
            }
        }

        private void _ReadThread()
        {
            string data = "";
            while((Connected == true) &&
                  ((data = _Reader.ReadLine()) != null)) {
                _ThreadReadBufferStreamWriter.WriteLine(data);
                _ThreadReadBufferStreamWriter.Flush();
                Logger.Socket.Debug("thread received: \""+data+"\"");
            }

            Logger.Socket.Warn("detected dead connection (socket returned null)");
            Disconnect();
        }
    }
}