Module Server
    Public ReadOnly ProcessFile As String = IO.Path.GetFullPath(Environment.GetCommandLineArgs()(0))
    Public ReadOnly ProcessDirectory As String = ProcessFile.Replace(IO.Path.GetFileName(ProcessFile), String.Empty)
    Public ReadOnly ConfigFile As String = ProcessDirectory & "scoutConfig.json"

    Public ReadOnly Json As New Web.Script.Serialization.JavaScriptSerializer
    Public Config As ConfigData

    Public TcpServer As Net.Sockets.TcpListener
    Public MessageLog As New List(Of Tuple(Of String, Console.Color))
    Public ShutdownRequested As Boolean = False
    Public ServerInstance As Threading.Thread

    Sub Main()
        Console.SetColor(Console.Color.White)
        While Not ShutdownRequested
            Try
                CheckConfig()
                If Not ShutdownRequested Then
                    StartServer()
                End If
                HandleMessages()
            Catch ex As Exception
                Console.SetColor(Console.Color.Red)
                Console.WriteLine("Unidentified fatal error: " & ex.Message)
                Console.SetColor(Console.Color.White)
            End Try
        End While
        StopThread()
    End Sub

    Public Sub HandleMessages()
        Try
            For Each msg As Tuple(Of String, Console.Color) In MessageLog
                Console.SetColor(msg.Item2)
                Console.WriteLine("Server [" & DateTime.Now & "]: " & msg.Item1)
                Console.SetColor(Console.Color.White)
            Next
            MessageLog.Clear()
        Catch
        End Try
    End Sub

    Public Sub StopThread()
        If ServerInstance IsNot Nothing Then
            Dim counter As Integer = 10
            While ServerInstance.ThreadState = Threading.ThreadState.Running And counter > 0
                Console.WriteLine("Server [" & DateTime.Now & "]: Waiting for threads to close... " & counter & "s")
                Threading.Thread.Sleep(1000)
                counter -= 1
            End While
            ServerInstance.Abort()
            ServerInstance = Nothing
        End If
    End Sub

    '
    ' Server Control
    '

    Public Sub StartServer()
        If ServerInstance Is Nothing Then
            Try
                If TcpServer IsNot Nothing Then
                    TcpServer.Stop()
                    TcpServer = Nothing
                End If
                TcpServer = New Net.Sockets.TcpListener(Net.IPAddress.Parse(Config.Host), Config.Port)
                TcpServer.Start()
                ServerInstance = New Threading.Thread(AddressOf ServerThread)
                ServerInstance.Start()
                Console.WriteLine("Server: Started at " & DateTime.Now)
            Catch ex As Exception
                Console.SetColor(Console.Color.Red)
                Console.WriteLine("Error starting server: " & ex.Message)
                Console.SetColor(Console.Color.White)
                ShutdownRequested = True
            End Try
        End If
    End Sub

    Public Sub StopServer()
        If ServerInstance IsNot Nothing Then
            If TcpServer IsNot Nothing Then
                TcpServer.Stop()
                TcpServer = Nothing
            End If
            ShutdownRequested = True
        End If
    End Sub

    Public Sub ServerThread()
        While Not ShutdownRequested
            Try
                GC.Collect()
                Dim clnt As Net.Sockets.TcpClient = TcpServer.AcceptTcpClient()
                Dim buffer As New List(Of Byte)
                While clnt.Connected
                    Try
                        If clnt.Available Then
                            Dim val As Integer = clnt.GetStream().ReadByte()
                            If -1 < val < 256 Then
                                buffer.Add(val)
                            End If
                        End If
                    Catch ex As Exception
                        MessageLog.Add(New Tuple(Of String, Console.Color)("Server error: " & ex.Message, Console.Color.Yellow))
                    End Try
                End While
                Dim data As String = Text.Encoding.UTF8.GetString(buffer.ToArray())
                Json.Deserialize(Of ClientDataRequest)(data) 'TODO
            Catch ex As Exception
                Console.SetColor(Console.Color.Yellow)
                Console.WriteLine("Server Error: " & ex.Message)
                Console.SetColor(Console.Color.White)
            End Try
        End While
    End Sub

    Public Class ClientDataRequest
        Public Property Type As Byte = 0
        Public Property Username As String = String.Empty
        Public Property PasswordHash As String = String.Empty
        Public Property Table As New List(Of ClientDataEntry)
    End Class

    Public Class ClientDataEntry
        Public Property Name As String = String.Empty
        Public Property Value As String = String.Empty
    End Class

    '
    ' Configuration
    '

    Public Sub CheckConfig()
        If Config Is Nothing Then
            If IO.File.Exists(ConfigFile) Then
                ReadConfig()
            Else
                Config = New ConfigData
                SaveConfig()
                Console.SetColor(Console.Color.Yellow)
                Console.WriteLine("Config Warning: Running with deafults. Please update the config file.")
                Console.SetColor(Console.Color.White)
            End If
        End If
    End Sub

    Public Sub SaveConfig()
        If Config IsNot Nothing Then
            Dim data As String = Json.Serialize(Config)
            Try
                IO.File.WriteAllText(ConfigFile, data)
            Catch ex As Exception
                Console.SetColor(Console.Color.Yellow)
                Console.WriteLine("Error saving config: " & ex.Message)
                Console.SetColor(Console.Color.White)
            End Try
        End If
    End Sub

    Public Sub ReadConfig()
        If IO.File.Exists(ConfigFile) Then
            Try
                Config = Json.Deserialize(Of ConfigData)(IO.File.ReadAllText(ConfigFile))
            Catch ex As Exception
                Console.SetColor(Console.Color.Red)
                Console.WriteLine("Error reading config: " & ex.Message)
                Console.SetColor(Console.Color.White)
                ShutdownRequested = True
            End Try
        End If
    End Sub

    Class ConfigData
        Public Property Host As String = "127.0.0.1"
        Public Property Port As Integer = 1600
        Public Property ApiKey As String = GetNewApiKey()

        Public Shared Function GetNewApiKey() As String
            Dim str As New Text.StringBuilder
            Dim rand As New Random
            For i As Integer = 0 To 63
                str.Append(rand.Next(0, 10))
            Next
            Return str.ToString()
        End Function
    End Class

    '
    ' Console Override
    '

    Class Console
        Public Shared Sub SetColor(color As Color)
            If color = Color.White Then
                System.Console.BackgroundColor = ConsoleColor.Black
                System.Console.ForegroundColor = ConsoleColor.White
            ElseIf color = Color.Yellow Then
                System.Console.BackgroundColor = ConsoleColor.Black
                System.Console.ForegroundColor = ConsoleColor.Yellow
            ElseIf color = Color.Red Then
                System.Console.BackgroundColor = ConsoleColor.White
                System.Console.ForegroundColor = ConsoleColor.Red
            End If
        End Sub

        Public Shared Sub WriteLine(message As String, ParamArray args() As String)
            For i As Integer = 0 To args.Length - 1
                message = message.Replace("{" & i & "}", args(i))
            Next
            System.Console.WriteLine(message)
            Debug.WriteLine(message)
        End Sub

        Public Enum Color
            White = 0
            Yellow = 1
            Red = 2
        End Enum
    End Class
End Module