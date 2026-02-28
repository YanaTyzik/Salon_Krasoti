using System.Xml.Serialization;

namespace LisBlanc.AdminPanel.Models
{
    [XmlRoot("AppointmentRequest")]
    public class XmlAppointmentRequest
    {
        
       
            [XmlElement("MasterId")]
            public int MasterId { get; set; }

            [XmlElement("ServiceId")]
            public int ServiceId { get; set; }

            [XmlElement("ClientName")]
            public string ClientName { get; set; }

            [XmlElement("ClientPhone")]
            public string ClientPhone { get; set; }

            [XmlElement("RequestedDateTime")]
            public DateTime RequestedDateTime { get; set; }
        
    }
}
