About
=====

Netstrings.cs is an implementation of the netstrings protocol (<a href="http://cr.yp.to/proto/netstrings.txt">http://cr.yp.to/proto/netstrings.txt</a>)

You can emit or decode single netstrings with the static methods <code>NetstringWriter.Encode(string value)</code> and <code>NetstringReader.Decode(string value)</code>.

You can also use the class to decode a stream of netstrings (such as a network socket):

	using System;
	using System.IO;
	using System.Net;
	using System.Net.Sockets;
	using System.Threading;

	namespace Netstrings
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
					
					foreach (string s in new NetstringReader(new StreamReader(stream)))
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

					NetstringWriter writer = new NetstringWriter(new StreamWriter(stream));

					string input = null;

					while (input != String.Empty)
					{
						input = Console.ReadLine();

						writer.Write(input);
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

The library is a bit weird and asymmetrical and I'll probably change my mind about how it all fits together at some point, but it works and it seems to be quite fast. Hopefully someone else can get some use out of it too.

Released under the MIT licence.