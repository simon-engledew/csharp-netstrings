About
=====

Netstrings.cs is an implementation of the netstrings protocol ( http://cr.yp.to/proto/netstrings.txt )

You can emit or decode single netstrings with the static methods <code>Netstrings.Encode(string value)</code> and <code>Netstrings.Decode(string value)</code>.

You can also use the class to decode a stream of netstrings (such as a network socket):

	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.IO;
	using System.Net.Sockets;
	using System.Net;
	using System.Threading;

	namespace Crypto
	{
		class Program
		{
			static void Main(string[] args)
			{
				EndPoint endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 3000);

				Thread serverThread = new Thread(() =>
				{
					Socket server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);
					
					server.Bind(endpoint);
					server.Listen(1);

					Socket client = server.Accept();

					NetworkStream stream = new NetworkStream(client);
					
					foreach (string s in new Netstrings(new StreamReader(stream)))
					{
						Console.WriteLine(s);
					}

					server.Close();
				});

				serverThread.Start();


				Thread clientThread = new Thread(() =>
				{
					Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.IP);

					client.Connect(endpoint);

					NetworkStream stream = new NetworkStream(client);

					StreamWriter writer = new StreamWriter(stream);

					string input = null;

					while (input != String.Empty)
					{
						input = Console.ReadLine();

						writer.Write(Netstrings.Encode(input));
						writer.Flush();
					}

					client.Close();

				});

				clientThread.Start();

				serverThread.Join();
				clientThread.Join();
			}
		}
	}
