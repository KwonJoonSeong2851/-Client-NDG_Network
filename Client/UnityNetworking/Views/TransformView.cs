
namespace NDG.UnityNet
{
    using UnityEngine;
    using Vector3 = UnityEngine.Vector3;
    using Quaternion = UnityEngine.Quaternion;

    public class TransformView : MonoBehaviourNet, INetObservable
    {
        private float distance;
        private float angle;

        private Vector3 direction;
        private Vector3 networkPosition;
        private Vector3 storedPosition;

        private Quaternion networkRotation;

        public bool synchronizePosition = true;
        public bool synchronizeRotation = true;
        public bool synchronizeScale = false;

        public bool useLocal = false;


        private bool first = false;

        public void Awake()
        {
            storedPosition = transform.localPosition;
            networkPosition = Vector3.zero;
            networkRotation = Quaternion.identity;
        }

        public void OnEnable()
        {
            first = true;
        }

        public void Update()
        {
            if (!this.networkView.IsMine)
            {
                if (useLocal)
                {
                    transform.localPosition = Vector3.MoveTowards(transform.localPosition, this.networkPosition, this.distance * Time.deltaTime * NDG_Network.SerializationRate);
                    transform.localRotation = Quaternion.RotateTowards(transform.localRotation, this.networkRotation, this.angle * Time.deltaTime * NDG_Network.SerializationRate);
                }
                else
                {
                    transform.position = Vector3.MoveTowards(transform.localPosition, this.networkPosition, this.distance * Time.deltaTime * NDG_Network.SerializationRate);
                    transform.rotation = Quaternion.RotateTowards(transform.localRotation, this.networkRotation, this.angle * Time.deltaTime * NDG_Network.SerializationRate);
                }
            }
        }

        public void OnNetSerializeView(NetworkStream stream, NetworkMessageInfo info)
        {
            if (stream.IsWriting)
            {
                //Position
                if (this.synchronizePosition)
                {
                    this.direction = transform.localPosition - this.storedPosition;
                    this.storedPosition = transform.localPosition;

                    stream.SendNext(transform.localPosition);
                    stream.SendNext(this.direction);
                }

                //Rotation
                if (this.synchronizeRotation)
                {
                    stream.SendNext(transform.localRotation);
                }

                //Scale
                if (this.synchronizeScale)
                {
                    stream.SendNext(transform.localScale);
                }
            }
            else
            {
                //Position
                if (this.synchronizePosition)
                {
                    this.networkPosition = (Vector3)stream.ReceiveNext();
                    this.direction = (Vector3)stream.ReceiveNext();

                    if (first)
                    {
                        if (useLocal)
                            transform.localPosition = this.networkPosition;
                        else
                            transform.position = this.networkPosition;
                        this.distance = 0f;
                    }
                    else
                    {
                        float lag = Mathf.Abs((float)(NDG_Network.Time - info.SentServerTime));
                        this.networkPosition += this.direction * lag;
                        if (useLocal)
                        {
                            this.distance = Vector3.Distance(transform.localPosition, this.networkPosition);
                        }
                        else
                        {
                            this.distance = Vector3.Distance(transform.position, this.networkPosition);
                        }
                    }
                }

                //Rotation
                if (this.synchronizeRotation)
                {
                    this.networkRotation = (Quaternion)stream.ReceiveNext();

                    if (first)
                    {
                        this.angle = 0f;
                        transform.localRotation = networkRotation;
                    }
                    else
                    {
                        if (useLocal)
                            this.angle = Quaternion.Angle(transform.localRotation, this.networkRotation);
                        else
                            this.angle = Quaternion.Angle(transform.rotation, this.networkRotation);


                    }
                }

                //Scale
                if (this.synchronizeScale)
                {
                    transform.localScale = (Vector3)stream.ReceiveNext();
                }

                if (first)
                {
                    first = false;
                }
            }
        }


    }
}
