# Rich Notifications Test App

- Register app on azure portal to obtain `AppId`, `AppSecret`, and `TenantId` values to be placed in appsettings.json
- Ensure the app has the the `ChannelMessage.Read.All` application permission added to it on the azure portal.

- Setup ngrok to route traffic to the local app url http://localhost:5000/ by running 
```
ngrok http 5000
```
then add the https url to appsettings.json under the `Ngrok`

- Generate a pfx certificate and place its path and password in the appsettings file(`CertificatePath` and `CertificatePassword`).

- Run the app
- Go to `http://localhost:5000/api/notifications` on your browser to start listening for events. The response should have the expiration time and the subsciption Id.
- Send a teams message in the tenant.
- You should now see the notifications streaming in the console while the subscription is still valid.
