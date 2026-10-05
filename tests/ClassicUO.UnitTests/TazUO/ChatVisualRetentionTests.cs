using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Serialization;
using ClassicUO.Game.Data;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Utility.Collections;
using Xunit;

namespace ClassicUO.UnitTests.TazUO
{
    public class ChatVisualRetentionTests
    {
        [Fact]
        public void Filtered_out_incoming_message_still_evicts_expired_visual_rows()
        {
            var store = new ChatHistoryStore((MessageType _) => true, 2);
            store.Add(new ChatHistoryRecord("Player", "Visible", 0, DateTime.MinValue, MessageType.Regular));
            Type lineType = typeof(BaseChatGump).GetNestedType("ChatLine", BindingFlags.NonPublic);
            Type bodyType = typeof(BaseChatGump).GetNestedType("ChatBody", BindingFlags.NonPublic);
            object body = FormatterServices.GetUninitializedObject(bodyType);
            var lines = (IList)Activator.CreateInstance(typeof(Deque<>).MakeGenericType(lineType));
            object line = Activator.CreateInstance(lineType, true);
            lineType.GetField("Record").SetValue(line, System.Linq.Enumerable.Single(store.All()));
            lines.Add(line);
            Set(bodyType, body, "_store", store);
            Set(bodyType, body, "_lines", lines);
            Set(bodyType, body, "_scrollBar", FormatterServices.GetUninitializedObject(typeof(ScrollBar)));
            Set(bodyType, body, "_recordFilter", new Predicate<ChatHistoryRecord>(r => r.Text == "Visible"));
            Set(bodyType, body, "_activeFilter", string.Empty);
            Set(bodyType, body, "_activeFontSize", 16);

            // No font/GPU setup: the existing row has no text boxes, and incoming
            // records do not match the view. Exercise the actual eviction path.
            var hidden = new ChatHistoryRecord("Player", "Hidden", 0, DateTime.MinValue, MessageType.Regular);
            store.Add(hidden);
            bodyType.GetMethod("AppendRecord").Invoke(body, new object[] { hidden, 16, string.Empty });
            Assert.Equal(1, lines.Count);
            store.Add(hidden);
            bodyType.GetMethod("AppendRecord").Invoke(body, new object[] { hidden, 16, string.Empty });
            Assert.Empty(lines);
        }

        private static void Set(Type type, object target, string field, object value) =>
            type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
