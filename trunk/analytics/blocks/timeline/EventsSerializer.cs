﻿using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using SC = LogJoint.Analytics.Timeline.SerializationCommon;

namespace LogJoint.Analytics.Timeline
{
	public class EventsSerializer: IEventsVisitor
	{
		public EventsSerializer(Action<object, XElement> triggerSerializer = null)
		{
			this.triggerSerializer = triggerSerializer;
		}

		public IEnumerable<XElement> Output { get { return output; } }


		void IEventsVisitor.Visit(ProcedureEvent evt)
		{
			CreateActivityElement(evt, SC.Elt_Procedure);
		}

		void IEventsVisitor.Visit(ObjectLifetimeEvent evt)
		{
			CreateActivityElement(evt, SC.Elt_Lifetime);
		}

		void IEventsVisitor.Visit(UserActionEvent evt)
		{
			CreateEventElement(evt, SC.Elt_UserAction);
		}

		void IEventsVisitor.Visit(NetworkMessageEvent evt)
		{
			var e = CreateActivityElement(evt, SC.Elt_NetworkMessage);
			e.SetAttributeValue(SC.Attr_Direction, (int)evt.Direction);
		}

		void IEventsVisitor.Visit(APICallEvent evt)
		{
			CreateEventElement(evt, SC.Elt_APICall);
		}

		void IEventsVisitor.Visit(EndOfTimelineEvent evt)
		{
			CreateEventElement(evt, SC.Elt_EOF);
		}

		static XAttribute MakeNullableAttr(string attrName, object value)
		{
			if (value == null)
				return null;
			return new XAttribute(attrName, value);
		}

		XElement CreateEventElement(Event evt, string name, params XAttribute[] attrs)
		{
			var element = new XElement(name, attrs.Concat(new[] { 
				MakeNullableAttr(SC.Attr_DisplayName, evt.DisplayName),
				new XAttribute(SC.Attr_Tags, string.Join(" ", evt.Tags))
			}).Where(a => a != null).ToArray());
			if (evt.Trigger != null && triggerSerializer != null)
				triggerSerializer(evt.Trigger, element);
			output.Add(element);
			return element;
		}

		XElement CreateActivityElement(ActivityEventBase evt, string name, params XAttribute[] attrs)
		{
			return CreateEventElement(evt, name, attrs.Concat(new[]
			{
				MakeNullableAttr(SC.Attr_ActivityId, evt.ActivityId),
				MakeNullableAttr(SC.Attr_Type, (int)evt.Type)
			}).ToArray());
		}

		readonly List<XElement> output = new List<XElement>();
		readonly Action<object, XElement> triggerSerializer;
	}
}
