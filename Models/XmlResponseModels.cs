using System.Xml.Serialization;
using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace LisBlanc.AdminPanel.Models
{
    // Ответ при успешном создании заявки
    [XmlRoot("Response")]
    public class CreateRequestSuccessResponse
    {
        [XmlElement("Status")]
        public string Status { get; set; }

        [XmlElement("Message")]
        public string Message { get; set; }

        [XmlElement("RequestId")]
        public int RequestId { get; set; }

        [XmlElement("Timestamp")]
        public DateTime Timestamp { get; set; }
    }

    // Ответ при ошибке
    [XmlRoot("Error")]
    public class ErrorResponse
    {
        [XmlElement("Status")]
        public string Status { get; set; }

        [XmlElement("Message")]
        public string Message { get; set; }

        [XmlElement("Timestamp")]
        public DateTime Timestamp { get; set; }
    }

    // Ответ при проверке статуса заявки
    [XmlRoot("RequestStatus")]
    public class RequestStatusResponse
    {
        [XmlElement("Id")]
        public int Id { get; set; }

        [XmlElement("Status")]
        public string Status { get; set; }

        [XmlElement("ClientName")]
        public string ClientName { get; set; }

        [XmlElement("RequestedDateTime")]
        public DateTime RequestedDateTime { get; set; }

        [XmlElement("MasterName")]
        public string MasterName { get; set; }

        [XmlElement("ServiceName")]
        public string ServiceName { get; set; }
    }

    // Список свободных слотов
    [XmlRoot("AvailableSlots")]
    public class AvailableSlotsResponse
    {
        [XmlElement("MasterId")]
        public int MasterId { get; set; }

        [XmlElement("Date")]
        public DateTime Date { get; set; }

        [XmlArray("Slots")]
        [XmlArrayItem("Slot")]
        public List<TimeSlotInfo> Slots { get; set; }
    }

    public class TimeSlotInfo
    {
        [XmlElement("StartTime")]
        public DateTime StartTime { get; set; }

        [XmlElement("EndTime")]
        public DateTime EndTime { get; set; }

        [XmlElement("Available")]
        public bool Available { get; set; }
    }
}