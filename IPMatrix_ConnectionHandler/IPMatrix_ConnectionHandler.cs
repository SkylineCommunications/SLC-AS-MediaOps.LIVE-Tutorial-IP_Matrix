namespace IPMatrixConnectionHandler
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Skyline.DataMiner.Automation;
    using Skyline.DataMiner.Solutions.MediaOps.Live.API.Enums;
    using Skyline.DataMiner.Solutions.MediaOps.Live.Automation.Mediation.ConnectionHandlers;
    using Skyline.DataMiner.Solutions.MediaOps.Live.Mediation.ConnectionHandlers.Data;

    public class Script : ConnectionHandlerScript
	{
		public override IEnumerable<ElementInfo> GetSupportedElements(IEngine engine, IEnumerable<ElementInfo> elements)
		{
			return elements.Where(e => e.Protocol == "Generic Dynamic Table");
		}

		public override IEnumerable<SubscriptionInfo> GetSubscriptionInfo(IEngine engine)
		{
			return new[]
			{
				new SubscriptionInfo(SubscriptionInfo.ParameterType.Table, 200), // Entries Table
			};
		}

		public override void ProcessParameterUpdate(IEngine engine, IConnectionHandlerEngine connectionEngine, ParameterUpdate update)
		{
			if (update.ParameterId != 200)
			{
				// we are only interested in updates of the outputs table
				return;
			}

			var updatedConnections = new List<ConnectionUpdate>();

			var elementId = update.DmsElementId;
			var destinationEndpoint = connectionEngine.Api.Endpoints.GetByElement(elementId)
				.SingleOrDefault(e => e.Role == EndpointRole.Destination);

			if (destinationEndpoint == null)
			{
				return;
				// throw new InvalidOperationException($"Element with ID {elementId} does not have a destination endpoint.");
			}

			if (update.UpdatedRows != null)
			{
				var row = update.UpdatedRows.Values.FirstOrDefault(x => String.Equals(x[1], "IP In"));
				if (row == null)
				{
					// No 'IP In' row found in the updated rows.
					return;
				}

				var multicastIp = Convert.ToString(row[4]);
				var isConnected = !String.IsNullOrWhiteSpace(multicastIp);

				if (isConnected)
				{
					var sourceEndpoint = connectionEngine.Api.Endpoints.GetByTransportMetadata("Multicast IP", multicastIp)
						.SingleOrDefault();

					if (sourceEndpoint != null)
					{
						// register connection between source and destination
						updatedConnections.Add(new ConnectionUpdate(sourceEndpoint, destinationEndpoint));
					}
					else
					{
						// source endpoint not found, register as connected to an unknown source
						updatedConnections.Add(new ConnectionUpdate(destinationEndpoint, isConnected: true));
					}
				}
				else
				{
					// register destination as disconnected
					updatedConnections.Add(new ConnectionUpdate(destinationEndpoint, isConnected: false));
				}
			}

			if (update.DeletedRows != null)
			{
				// not implemented for this example
			}

			if (updatedConnections.Count > 0)
			{
				connectionEngine.RegisterConnections(updatedConnections);
			}
		}

		public override void Connect(IEngine engine, IConnectionHandlerEngine connectionEngine, CreateConnectionsRequest createConnectionsRequest)
		{
			var groupedByDestinationElement = createConnectionsRequest.Connections.GroupBy(x => x.DestinationEndpoint.Element);

			foreach (var group in groupedByDestinationElement)
			{
				var elementId = group.Key.Value;
				var element = engine.FindElement(elementId.AgentId, elementId.ElementId);

				foreach (var connection in group)
				{
					var endpoint = connection.SourceEndpoint;
					var multicastIP = endpoint.GetTransportMetadata("Multicast IP");

					// Connect by setting the multicast IP in the "IP In" row of the Entries table
					element.SetParameter("Text Value (Entries)", "IP In", multicastIP);
				}
			}
		}

		public override void Disconnect(IEngine engine, IConnectionHandlerEngine connectionEngine, DisconnectDestinationsRequest disconnectDestinationsRequest)
		{
			var groupedByDestinationElement = disconnectDestinationsRequest.Destinations.GroupBy(x => x.Element);

			foreach (var group in groupedByDestinationElement)
			{
				var elementId = group.Key.Value;
				var element = engine.FindElement(elementId.AgentId, elementId.ElementId);

				foreach (var destination in group)
				{
					// Disconnect by clearing the multicast IP
					element.SetParameter("Text Value (Entries)", "IP In", String.Empty);
				}
			}
		}
	}
}
