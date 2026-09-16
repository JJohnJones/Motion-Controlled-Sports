# Phone controller screens

The existing Bootstrap flow now delivers GameDefinition.controllerUiMode through an optional IControllerUiPresenter implemented by ControllerLobbyAdapter. SceneFlowManager never accesses WebRTC directly. ControllerProtocolRouter caches presentation state and replays it on authenticated joins and resumed input, over IControllerTransport.

Pairing/menu/results use setup or waiting on the phone. Bowling uses `bowling`; Busy transitions and pause send `paused: true`, canceling touch controls until play resumes. Sensor processing, player identities and gameplay mechanics are unchanged. Mode delivery does not require any new components, scene generation or Inspector configuration.

Deploy the matching separate Motion Controller Website repository changes; signaling needs no update. See that repository's README for mode definitions, debug access, service-worker refresh and real-phone acceptance steps. Bowling maps the whole phone viewport to the existing `primary` action. Future 2–4-zone layouts are supported by the PWA renderer; future action IDs must be added to the Unity input protocol when their games are implemented.
