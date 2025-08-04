using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Gravitas.UI
{
    /// <summary>
    /// UI MonoBehaviour class for updating various text displays about a subject.
    /// </summary>
    public sealed class GravitasStatsUI : MonoBehaviour
    {
        private static readonly StringBuilder stringBuilder = new StringBuilder(64);

        [SerializeField] private GravitasSubject gravitasSubject;
        [SerializeField] private Text landedText, statValueText;

        public void SetSubject (GravitasSubject newSubject)
        {
            gravitasSubject = newSubject;
        }

        void Awake()
        {
            // Warning if any of the text fields are not assigned
            if (!gravitasSubject)
                Debug.LogWarning($"Gravitas: Stats UI object {gameObject.name} gravitas subject field not assigned!");

            if (!statValueText)
            {
                Debug.LogWarning($"Gravitas: Stats UI object {gameObject.name} stat value field not assigned!");

                Destroy(this);
            }
        }

        void FixedUpdate()
        {
            if (!gravitasSubject || !gravitasSubject.gameObject.activeInHierarchy) { return; }

            stringBuilder.Clear();

            float speed = gravitasSubject.GravitasBody.Velocity.magnitude;
            if (speed < 0.1f)
                speed = 0;

            stringBuilder.AppendFormat("{0:0.0} m/s", speed);

            IGravitasField field = gravitasSubject.CurrentField;
            if (field != null)
            {
                float distanceMultiplier = field.GetDistanceMultiplier(gravitasSubject.GravitasBody.ProxyPosition);
                float gForce = field.Acceleration * distanceMultiplier / 9.81f;

                stringBuilder.AppendFormat("\n{0:0.0}g", gForce);
                stringBuilder.AppendFormat("\n{0}", field.GameObject.name);

                if (distanceMultiplier > 0)
                    stringBuilder.AppendFormat("\n{0:0.0}", distanceMultiplier);
                else
                    stringBuilder.Append('\n');
            }
            else
            {
                stringBuilder.Append("\n\n\n");
            }

            statValueText.text = stringBuilder.ToString();

            if (landedText)
            {
                landedText.enabled = gravitasSubject.GravitasBody.IsLanded;
            }
        }
    }
}
