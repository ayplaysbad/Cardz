# Online Quick Match Setup

This project is moving away from direct-IP testing. The player-facing online route is:

1. Choose `Online Quick Match`.
2. Choose `Free Haven` or `Iron Citadel`.
3. Unity Lobby finds a waiting opponent using the opposite city.
4. Unity Relay connects the two clients.
5. The existing action/snapshot match sync runs the game.

## Unity Editor Setup

- Open `Project Settings > Services` and link the project to a Unity Cloud project.
- Ensure Unity Gaming Services is enabled for the linked project.
- In the Unity Dashboard, enable Authentication, Lobby, and Relay.
- Anonymous Authentication is enough for this first testing pass.
- Let Package Manager resolve:
  - `com.unity.services.core`
  - `com.unity.services.authentication`
  - `com.unity.services.lobby`
  - `com.unity.services.relay`

## Test Flow

- Use `Turn-Based Test` for local solo testing.
- Use `Online Quick Match` for two-client testing.
- For the first online pass, same-city mirrors are intentionally blocked: Free Haven matches Iron Citadel only.
- If matchmaking fails immediately, check that the project is linked to Unity Services and that Authentication/Lobby/Relay are enabled in the dashboard.
