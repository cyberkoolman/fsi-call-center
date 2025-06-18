import streamlit as st
import requests
import uuid  # To generate unique conversation IDs

st.set_page_config(
    page_title="Contact Center Chat Demo",
    page_icon="🤖",
    layout="wide"
)

st.title("Contact Center Chat Demo")

# API base URL
API_URL = "http://localhost:5108/Question"  # Replace with your API base URL if different

# Initialize conversation ID
if "conversation_id" not in st.session_state:
    st.session_state.conversation_id = str(uuid.uuid4())  # Generate a unique conversation ID

# check for messages in session and create if not exists
if "messages" not in st.session_state.keys():
    st.session_state.messages = [
        {"role": "assistant", "content": "Hello there, I am a customer support for Contoso, how can I help you today?"}
    ]

# Display all messages
for message in st.session_state.messages:
    with st.chat_message(message["role"]):
        st.write(message["content"])

user_prompt = st.chat_input("Type your message here ...")


if user_prompt is not None:
    # Append user's message to session state
    st.session_state.messages.append({"role": "user", "content": user_prompt})
    with st.chat_message("user"):
        st.write(user_prompt)

    # Prepare the payload for the API
    payload = { "question": user_prompt,
                "conversationId": st.session_state.conversation_id,  # Pass conversation ID
    }

    # Call the API
    with st.chat_message("assistant"):
        with st.spinner("Thinking..."):
            try:
                # Make a POST request to your API
                response = requests.post(API_URL, json=payload)
                response.raise_for_status()  # Raise an error for bad HTTP status codes
                api_response = response.json()  # Parse the JSON response
                
                # Extract the assistant's response text from the API response
                ai_response = (
                    api_response.get("answer", {})
                    .get("items", [{}])[0]  # Access the first item in the items array
                    .get("text", "I couldn't find an answer. Please try again.")
                )
                st.write(ai_response)
                
                # Append the assistant's response to session state
                st.session_state.messages.append({"role": "assistant", "content": ai_response})

            except requests.exceptions.RequestException as e:
                st.error(f"An error occurred: {e}")