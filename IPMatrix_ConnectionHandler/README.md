# IPMatrix_ConnectionHandler

## Summary

Automation script that serves as a connection handler for integrating an IP Matrix connector with the MediaOps Live solution. It subscribes to table parameter updates from supported elements and detects connections between source and destination endpoints by matching multicast IP transport metadata. When a connection change is detected, it registers the updated connection state with the MediaOps Live API.

## Project Type

Automation Script

## Input Arguments

| Parameter | Type | Description |
|---|---|---|
| Action | Text | The action to be performed by the connection handler. |
| Input Data | Text | The input data associated with the action. |
