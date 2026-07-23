1. Put Html content into `content` field.  Update "address" with target recipient.

   Target JSON:

   ```
   {
      "message": {
          "subject": "Test",
          "body": {
              "contentType": "html",
              "content": ""
          },
          "toRecipients": [
              {
                  "emailAddress": {
                      "address": "<<TARGET_EMAIL_ADDRESS>>"
                  }
              }
          ]
      }
   }
   ```

2. Adaptive Card wrapped in HTML content.  Need to know the Outlook Provider Id and update the `originator` property:

   ```
   <html>

   <head>
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8">
    <script type="application/adaptivecard+json">{
      "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
      "type": "AdaptiveCard",
      "originator":"<<OUTLOOK_PROVIDER_ID>>",
      "version": "1.4",
      "body": [
        {
          "type": "TextBlock",
          "text": "Present a form and submit it back to the originator"
        },
        {
          "type": "Input.Text",
          "id": "firstName",
          "placeholder": "What is your first name?"
        },
        {
          "type": "Input.Text",
          "id": "lastName",
          "placeholder": "What is your last name?"
        },
        {
          "type": "ActionSet",
          "actions": [
            {
              "type": "Action.Execute",
              "title": "Submit",
              "verb": "personalDetailsFormSubmit"
            }
          ]
        }
      ]
    }
    </script>
   </head>

  <body>
    Visit the <a href="https://learn.microsoft.com/outlook/actionable-messages">Outlook Dev Portal</a> to learn more about
    Actionable Messages.
  </body>

  </html>
  ```

3. Use Graph Explorer to POST to `https://graph.microsoft.com/v1.0/me/sendMail`:
   ```
   {
    "message": {
      "subject": "Test",
      "body": {
        "contentType": "html",
        "content": "<html>\n\n<head>\n  <meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\">\n  <script type=\"application/adaptivecard+json\">{\n  \"$schema\": \"http://adaptivecards.io/schemas/adaptive-card.json\",\n  \"type\": \"AdaptiveCard\",\n  \"originator\":\"<<OUTLOOK_PROVIDER_ID>>\",\n  \"version\": \"1.4\",\n  \"body\": [\n    {\n      \"type\": \"TextBlock\",\n      \"text\": \"Present a form and submit it back to the originator\"\n    },\n    {\n      \"type\": \"Input.Text\",\n      \"id\": \"firstName\",\n      \"placeholder\": \"What is your first name?\"\n    },\n    {\n      \"type\": \"Input.Text\",\n      \"id\": \"lastName\",\n      \"placeholder\": \"What is your last name?\"\n    },\n    {\n      \"type\": \"ActionSet\",\n      \"actions\": [\n        {\n          \"type\": \"Action.Execute\",\n          \"title\": \"Submit\",\n          \"verb\": \"personalDetailsFormSubmit\"\n        }\n      ]\n    }\n  ]\n}\n    \n  </script>\n</head>\n\n<body>\n  Visit the <a href=\"https://learn.microsoft.com/outlook/actionable-messages\">Outlook Dev Portal</a> to learn more about\n  Actionable Messages.\n</body>\n\n</html>"
      },
      "toRecipients": [
        {
          "emailAddress": {
            "address": "<<TARGET_EMAIL_ADDRESS>>"
          }
        }
      ]
    }
   }
   ```