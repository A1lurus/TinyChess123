using System.Collections;
using System.Collections.Generic;
using System;
using Unity.Networking.Transport;
using Unity.Collections;
using UnityEngine;

public enum OpCode
{
    KEEP_ALIVE = 1,
    WELCOME = 2,
    START_GAME = 3,
    MAKE_MOVE = 4,
    REMATCH = 5
}

public class NetMessage
{
    public OpCode Code { set; get; }

    public virtual void Serialize(ref DataStreamWriter write)
    {
        write.WriteByte((byte)Code);
    }
    public virtual void Deserialize(DataStreamReader read)
    {

    }

    public virtual void ReceivedOnClient()
    {

    }
    public virtual void ReceivedOnServer(NetworkConnection cnn)
    {

    }
}
